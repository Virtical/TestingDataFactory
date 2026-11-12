using System.Globalization;
using System.Text.RegularExpressions;
using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;
using YamlDotNet.RepresentationModel;

namespace TestingDataFactory.Core.Specifications.Parsing;

internal sealed partial class YamlSpecificationParser
{
    private static readonly Regex IsoTime = new(
        @"\A(?<time>[0-9]{2}:[0-9]{2}(?::[0-9]{2}(?:\.[0-9]{1,7})?)?)(?<offset>Z|[+-][0-9]{2}:[0-9]{2})?\z",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private static TemporalBound? ReadTemporal(YamlMappingNode field, string name, string path,
        FieldType type, List<SpecificationDiagnostic> errors)
    {
        var node = Get(field, name);
        if (node is null) return null;
        if (node is not YamlScalarNode scalar || !IsString(scalar) || scalar.Value is not { } text ||
            !TryParseTemporal(text, type, out var value))
        {
            Error(errors, "INVALID_TEMPORAL_BOUND", "Ожидается корректная дата или время в формате ISO 8601.", path + "." + name, node);
            return null;
        }
        return value;
    }

    private static bool TryParseTemporal(string text, FieldType type, out TemporalBound? result)
    {
        result = null;
        DateOnly? date = null;
        TimeOnly? time = null;
        TimeSpan? offset = null;
        var remaining = text;
        if (type is FieldType.Date or FieldType.DateTime)
        {
            if (text.Length < 10 || !DateOnly.TryParseExact(text[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsedDate)) return false;
            date = parsedDate;
            remaining = text[10..];
            if (type == FieldType.Date)
            {
                if (remaining.Length != 0) return false;
                result = new(date, null, null);
                return true;
            }
            if (remaining.Length == 0)
            {
                result = new(date, TimeOnly.MinValue, null);
                return true;
            }
            if (remaining[0] != 'T') return false;
            remaining = remaining[1..];
        }

        var match = IsoTime.Match(remaining);
        if (!match.Success || !TimeOnly.TryParseExact(match.Groups["time"].Value,
                ["HH:mm", "HH:mm:ss", "HH:mm:ss.FFFFFFF"], CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsedTime)) return false;
        time = parsedTime;
        var zone = match.Groups["offset"].Value;
        if (zone == "Z") offset = TimeSpan.Zero;
        else if (zone.Length > 0)
        {
            var hours = int.Parse(zone.AsSpan(1, 2), CultureInfo.InvariantCulture);
            var minutes = int.Parse(zone.AsSpan(4, 2), CultureInfo.InvariantCulture);
            if (hours > 14 || minutes > 59 || (hours == 14 && minutes != 0)) return false;
            offset = new TimeSpan(hours, minutes, 0) * (zone[0] == '-' ? -1 : 1);
        }
        result = new(date, time, offset);
        return true;
    }

    private static void CheckTemporalRange(TemporalBound? min, TemporalBound? max, string path,
        YamlNode node, List<SpecificationDiagnostic> errors)
    {
        if (min is null || max is null) return;
        if (min.UtcOffset.HasValue != max.UtcOffset.HasValue)
        {
            Error(errors, "INCOMPARABLE_TEMPORAL_BOUNDS", "Обе границы должны одинаково указывать часовой пояс.", path, node);
            return;
        }
        static long Ticks(TemporalBound bound) =>
            (bound.Date?.ToDateTime(TimeOnly.MinValue).Ticks ?? 0) + (bound.Time?.Ticks ?? 0) - (bound.UtcOffset?.Ticks ?? 0);
        if (Ticks(min) > Ticks(max)) Error(errors, "INVALID_RANGE", "min не может быть больше max.", path, node);
    }
}
