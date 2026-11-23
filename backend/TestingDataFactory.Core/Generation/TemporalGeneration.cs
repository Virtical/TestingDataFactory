using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Generation;

internal static class TemporalGeneration
{
    private static readonly DateOnly DefaultStart = new(2000, 1, 1);
    private static readonly DateOnly DefaultEnd = new(2030, 12, 31);

    internal static (long Min, long Max, TimeSpan? Offset) Bounds(FieldDefinition field)
    {
        var offset = field.TemporalMin?.UtcOffset ?? field.TemporalMax?.UtcOffset;
        var (defaultMin, defaultMax) = field.Type switch
        {
            FieldType.Date => ((long)DefaultStart.DayNumber, (long)DefaultEnd.DayNumber),
            FieldType.Time => (0L, TimeSpan.TicksPerDay - 1),
            FieldType.DateTime => (DefaultStart.ToDateTime(TimeOnly.MinValue).Ticks, DefaultEnd.ToDateTime(TimeOnly.MaxValue).Ticks),
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };
        if (field.Type != FieldType.Date && offset is { } zone)
        {
            defaultMin -= zone.Ticks;
            defaultMax -= zone.Ticks;
        }
        var min = field.TemporalMin is { } minBound ? ToNumber(minBound, field.Type) : defaultMin;
        var max = field.TemporalMax is { } maxBound ? ToNumber(maxBound, field.Type) : defaultMax;
        if (field.TemporalMin is null) min = Math.Min(min, max);
        if (field.TemporalMax is null) max = Math.Max(max, min);
        return (min, max, offset);
    }

    private static long ToNumber(TemporalBound bound, FieldType type) => type switch
    {
        FieldType.Date => bound.Date!.Value.DayNumber,
        FieldType.Time => bound.Time!.Value.Ticks - (bound.UtcOffset?.Ticks ?? 0),
        FieldType.DateTime => bound.Date!.Value.ToDateTime(bound.Time!.Value).Ticks - (bound.UtcOffset?.Ticks ?? 0),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    internal static TemporalBound FromNumber(long value, FieldType type, TimeSpan? offset)
    {
        if (type == FieldType.Date) return new(DateOnly.FromDayNumber((int)value), null, null);
        var maxLocal = type == FieldType.Time ? TimeSpan.TicksPerDay - 1 : DateTime.MaxValue.Ticks;
        var local = value + (offset?.Ticks ?? 0);
        if (offset is not null && (local < 0 || local > maxLocal))
        {
            offset = value < 0 ? TimeSpan.FromHours(14) : value > maxLocal ? TimeSpan.FromHours(-14) : TimeSpan.Zero;
            local = value + offset.Value.Ticks;
        }
        if (type == FieldType.Time) return new(null, new TimeOnly(local), offset);
        var dateTime = new DateTime(local, DateTimeKind.Unspecified);
        return new(DateOnly.FromDateTime(dateTime), TimeOnly.FromDateTime(dateTime), offset);
    }
}
