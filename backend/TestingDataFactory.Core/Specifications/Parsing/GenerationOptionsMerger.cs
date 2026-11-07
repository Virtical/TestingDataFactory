using System.Collections.ObjectModel;
using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Specifications.Parsing;

/// <summary>Применяет параметры CLI или формы и повторно проверяет полученный результат.</summary>
public static class GenerationOptionsMerger
{
    public static GenerationOptionsResult Apply(Specification specification, GenerationOverrides overrides)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(overrides);
        var original = specification.Generation;
        var rows = new Dictionary<string, int>(StringComparer.Ordinal);
        if (original is not null) foreach (var entry in original.Rows) rows[entry.Key] = entry.Value;
        if (overrides.Rows is not null) foreach (var entry in overrides.Rows) rows[entry.Key] = entry.Value;
        var merged = new GenerationOptions(overrides.Seed ?? original?.Seed,
            new ReadOnlyDictionary<string, int>(rows), overrides.Mode ?? original?.Mode ?? GenerationMode.Valid,
            overrides.Output ?? original?.Output);
        var errors = Validate(merged, specification.Tables);
        return new(errors.Count == 0 ? merged : null, errors.AsReadOnly());
    }

    internal static List<SpecificationDiagnostic> Validate(GenerationOptions options, IReadOnlyDictionary<string, TableDefinition> tables)
    {
        var errors = new List<SpecificationDiagnostic>();
        if (!Enum.IsDefined(options.Mode)) errors.Add(new("INVALID_GENERATION_MODE", "Неизвестный режим генерации.", "$.generation.mode"));
        foreach (var (name, count) in options.Rows)
        {
            var path = "$.generation.rows['" + name.Replace("'", "\\'") + "']";
            if (count < 0) errors.Add(new("INVALID_ROW_COUNT", "Число строк должно быть неотрицательным.", path));
            if (!tables.TryGetValue(name, out var table))
                errors.Add(new("UNKNOWN_ROWS_TABLE", "Таблица не найдена.", path));
            else if (table.Fields.Values.Any(field => field.Reference is not null))
                errors.Add(new("ROWS_FOR_CHILD_TABLE", "Число строк задаётся только для корневых таблиц.", path));
        }
        switch (options.Output)
        {
            case null or JsonOutput: break;
            case CsvOutput csv when IsValidDelimiter(csv.Delimiter): break;
            case CsvOutput:
                errors.Add(new("INVALID_CSV_DELIMITER", "Разделитель должен быть одним символом, кроме кавычки и символов переноса строки.", "$.generation.output.csv.delimiter"));
                break;
            case SqlOutput sql:
                if (!Enum.IsDefined(sql.Dialect)) errors.Add(new("INVALID_SQL_DIALECT", "Неизвестный диалект SQL.", "$.generation.output.sql.dialect"));
                if (sql.Style is not null && !Enum.IsDefined(sql.Style.Value)) errors.Add(new("INVALID_SQL_STYLE", "Неизвестный стиль SQL.", "$.generation.output.sql.style"));
                break;
            default: errors.Add(new("INVALID_OUTPUT", "Неизвестный формат вывода.", "$.generation.output")); break;
        }
        return errors;
    }

    internal static bool IsValidDelimiter(char value) => value is not ('\0' or '\r' or '\n' or '"') && !char.IsSurrogate(value);
}
