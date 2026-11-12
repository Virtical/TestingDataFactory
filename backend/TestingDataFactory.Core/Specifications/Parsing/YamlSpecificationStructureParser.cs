using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace TestingDataFactory.Core.Specifications.Parsing;

/// <summary>Проверяет верхний уровень YAML, не проверяя содержимое разделов.</summary>
internal sealed class YamlSpecificationStructureParser
{
    private const int MaxLength = 1_000_000;
    private const int MaxDepth = 64;
    private static readonly HashSet<string> KnownSections = ["spec_version", "locale", "types", "tables", "generation"];

    public SpecificationStructureResult Parse(string yaml) => ParseCore(yaml, ignoreGeneration: false);

    internal SpecificationStructureResult ParseIgnoringGeneration(string yaml) => ParseCore(yaml, ignoreGeneration: true);

    private static SpecificationStructureResult ParseCore(string yaml, bool ignoreGeneration)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        var errors = new List<SpecificationDiagnostic>();
        if (yaml.Length > MaxLength)
        {
            errors.Add(new("DOCUMENT_TOO_LARGE", "Документ превышает допустимый размер.", "$"));
            return new(null, errors);
        }

        try
        {
            var parser = new Parser(new StringReader(yaml));
            var events = new List<ParsingEvent>();
            var depth = 0;
            while (parser.MoveNext())
            {
                var item = parser.Current!;
                events.Add(item);
                if (item is MappingStart or SequenceStart) depth++;
                if (item is MappingEnd or SequenceEnd) depth--;
                if (depth > MaxDepth)
                {
                    errors.Add(Diagnostic("MAX_DEPTH_EXCEEDED", "Превышена допустимая вложенность YAML.", "$", item));
                    return new(null, errors);
                }
                if (item is AnchorAlias)
                {
                    errors.Add(Diagnostic("ALIAS_NOT_SUPPORTED", "YAML-ссылки пока не поддерживаются.", "$", item));
                    return new(null, errors);
                }
                if (item is NodeEvent node && !node.Tag.IsEmpty)
                {
                    errors.Add(Diagnostic("TAG_NOT_SUPPORTED", "Явные YAML-теги пока не поддерживаются.", "$", item));
                    return new(null, errors);
                }
            }

            return ReadStructure(events, errors, ignoreGeneration);
        }
        catch (YamlException exception)
        {
            errors.Add(new("INVALID_YAML", "Некорректный синтаксис YAML: " + exception.Message, "$",
                exception.Start.Line, exception.Start.Column));
            return new(null, errors);
        }
        // Некоторые ошибки сканера YamlDotNet 13 сообщаются как System.Exception без позиции.
        catch (Exception exception) when (exception.GetType() == typeof(Exception))
        {
            errors.Add(new("INVALID_YAML", "Некорректный синтаксис YAML.", "$"));
            return new(null, errors);
        }
    }

    private static SpecificationStructureResult ReadStructure(List<ParsingEvent> events, List<SpecificationDiagnostic> errors, bool ignoreGeneration)
    {
        var documentCount = events.Count(item => item is DocumentStart);
        if (documentCount != 1)
        {
            errors.Add(new("DOCUMENT_COUNT", "Ожидается ровно один YAML-документ.", "$"));
            return new(null, errors);
        }

        var index = events.FindIndex(item => item is DocumentStart) + 1;
        if (events[index] is not MappingStart)
        {
            errors.Add(Diagnostic("ROOT_NOT_MAPPING", "Корень документа должен быть объектом.", "$", events[index]));
            return new(null, errors);
        }

        index++;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string? version = null;
        var locale = "ru_RU";
        while (events[index] is not MappingEnd)
        {
            var keyEvent = events[index];
            if (keyEvent is not Scalar key || string.IsNullOrWhiteSpace(key.Value))
            {
                errors.Add(Diagnostic("INVALID_SECTION_NAME", "Имя раздела должно быть непустой строкой.", "$", keyEvent));
                SkipNode(events, ref index);
                SkipNode(events, ref index);
                continue;
            }

            index++;
            var value = events[index];
            var path = "$.'" + key.Value + "'";
            if (!seen.Add(key.Value))
                errors.Add(Diagnostic("DUPLICATE_SECTION", "Раздел указан повторно.", path, key));
            else if (!KnownSections.Contains(key.Value))
                errors.Add(Diagnostic("UNKNOWN_SECTION", "Неизвестный раздел.", path, key));
            else if (key.Value == "spec_version")
            {
                if (value is not Scalar scalar || scalar.Style is not (ScalarStyle.SingleQuoted or ScalarStyle.DoubleQuoted))
                    errors.Add(Diagnostic("INVALID_VERSION", "Версия должна быть строкой в кавычках, например '1.0'.", path, value));
                else if (scalar.Value != "1.0")
                    errors.Add(Diagnostic("UNSUPPORTED_VERSION", "Поддерживается только версия '1.0'.", path, value));
                else version = scalar.Value;
            }
            else if (key.Value == "locale")
            {
                if (value is not Scalar scalar || string.IsNullOrWhiteSpace(scalar.Value) ||
                    (scalar.Style == ScalarStyle.Plain && (scalar.Value is "null" or "Null" or "NULL" or "~" ||
                     bool.TryParse(scalar.Value, out _) || decimal.TryParse(scalar.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))))
                    errors.Add(Diagnostic("INVALID_LOCALE", "Локаль должна быть непустой строкой.", path, value));
                else locale = scalar.Value;
            }
            else if (!(ignoreGeneration && key.Value == "generation") && value is not MappingStart)
                errors.Add(Diagnostic("SECTION_NOT_MAPPING", "Раздел должен быть объектом.", path, value));

            SkipNode(events, ref index);
        }

        if (!seen.Contains("spec_version")) errors.Add(new("MISSING_VERSION", "Не указана версия спецификации.", "$.spec_version"));
        if (!seen.Contains("tables")) errors.Add(new("MISSING_TABLES", "Не указан раздел tables.", "$.tables"));
        return errors.Count == 0
            ? new(new SpecificationStructure(version!, locale, Array.AsReadOnly(seen.ToArray())), errors.AsReadOnly())
            : new(null, errors.AsReadOnly());
    }

    private static void SkipNode(List<ParsingEvent> events, ref int index)
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

    private static SpecificationDiagnostic Diagnostic(string code, string message, string path, ParsingEvent item) =>
        new(code, message, path, item.Start.Line, item.Start.Column);
}
