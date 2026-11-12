using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace TestingDataFactory.Core.Specifications.Parsing;

/// <summary>Скрывает содержимое generation, сохраняя позиции остальных узлов YAML.</summary>
internal static class IgnoredGenerationSanitizer
{
    public static string Sanitize(string yaml)
    {
        // Размер и ошибки синтаксиса сообщает общий парсер.
        if (yaml.Length > 1_000_000) return yaml;
        try
        {
            var parser = new Parser(new StringReader(yaml));
            var events = new List<ParsingEvent>();
            while (parser.MoveNext()) events.Add(parser.Current!);
            var start = events.FindIndex(e => e is DocumentStart);
            if (start < 0 || start + 1 >= events.Count || events[start + 1] is not MappingStart) return yaml;

            var chars = yaml.ToCharArray();
            var index = start + 2;
            while (index < events.Count && events[index] is not MappingEnd)
            {
                var key = events[index];
                Skip(events, ref index);
                var valueStart = index;
                Skip(events, ref index);
                if (key is not Scalar { Value: "generation" }) continue;

                var value = events[valueStart];
                var last = events[index - 1];
                var from = value.Start.Index;
                var end = last.End.Index;
                if (value is MappingStart { Style: MappingStyle.Flow } or SequenceStart { Style: SequenceStyle.Flow }) end++;
                if (from >= end || from < 0 || end > chars.Length) continue;
                for (var i = from; i < end; i++)
                    if (chars[i] is not ('\r' or '\n')) chars[i] = ' ';
                chars[from] = '~';
            }
            return new string(chars);
        }
        catch (YamlException) { return yaml; }
        catch (Exception exception) when (exception.GetType() == typeof(Exception)) { return yaml; }
    }

    private static void Skip(List<ParsingEvent> events, ref int index)
    {
        if (events[index] is not (MappingStart or SequenceStart)) { index++; return; }
        var depth = 0;
        do
        {
            if (events[index] is MappingStart or SequenceStart) depth++;
            if (events[index] is MappingEnd or SequenceEnd) depth--;
            index++;
        } while (depth > 0);
    }
}
