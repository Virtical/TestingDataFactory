using System.Collections.ObjectModel;
using System.Globalization;
using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace TestingDataFactory.Core.Specifications.Parsing;

internal sealed partial class YamlSpecificationParser
{
    private static GenerationOptions? ReadGeneration(YamlNode? node, IReadOnlyDictionary<string, TableDefinition> tables,
        List<SpecificationDiagnostic> errors, bool validateReferences = true)
    {
        if (node is null) return null;
        if (node is not YamlMappingNode generation)
        {
            Error(errors, "INVALID_GENERATION", "Раздел generation должен быть объектом.", "$.generation", node);
            return null;
        }
        const string path = "$.generation";
        CheckKeys(generation, ["seed", "rows", "mode", "output"], path, errors);
        var seed = ReadInt(Get(generation, "seed"), path + ".seed", errors, "INVALID_SEED", allowNegative: true);
        var rows = new Dictionary<string, int>(StringComparer.Ordinal);
        var locations = new Dictionary<string, YamlNode>(StringComparer.Ordinal);
        if (Get(generation, "rows") is { } rowsNode)
        {
            if (rowsNode is not YamlMappingNode rowsMap)
                Error(errors, "INVALID_ROWS", "rows должен быть объектом с количеством строк для таблиц.", path + ".rows", rowsNode);
            else foreach (var (key, value) in rowsMap.Children)
            {
                var table = Name(key, path + ".rows", errors);
                if (table is null) continue;
                var rowPath = ChildPath(path + ".rows", table);
                var count = ReadInt(value, rowPath, errors, "INVALID_ROW_COUNT", allowNegative: false);
                locations[rowPath] = value;
                if (count is not null) rows.Add(table, count.Value);
            }
        }

        var mode = GenerationMode.Valid;
        if (Get(generation, "mode") is { } modeNode)
        {
            var value = Name(modeNode, path + ".mode", errors);
            switch (value)
            {
                case "valid": break;
                case "edge_cases": mode = GenerationMode.EdgeCases; break;
                case "anomalies": mode = GenerationMode.Anomalies; break;
                case not null: Error(errors, "INVALID_GENERATION_MODE", "Допустимые режимы: valid, edge_cases, anomalies.", path + ".mode", modeNode); break;
            }
        }
        var output = ReadOutput(Get(generation, "output"), path + ".output", errors);
        var options = new GenerationOptions(seed, new ReadOnlyDictionary<string, int>(rows), mode, output);
        foreach (var diagnostic in validateReferences ? GenerationOptionsMerger.Validate(options, tables) : [])
        {
            if (locations.TryGetValue(diagnostic.Path, out var source))
                errors.Add(diagnostic with { Line = source.Start.Line, Column = source.Start.Column });
            else errors.Add(diagnostic);
        }
        return options;
    }

    private static int? ReadInt(YamlNode? node, string path, List<SpecificationDiagnostic> errors, string code, bool allowNegative)
    {
        if (node is null) return null;
        if (node is YamlScalarNode scalar && scalar.Style == ScalarStyle.Plain &&
            int.TryParse(scalar.Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) &&
            (allowNegative || value >= 0)) return value;
        Error(errors, code, allowNegative ? "Ожидается целое число в диапазоне Int32." : "Ожидается неотрицательное целое число в диапазоне Int32.", path, node);
        return null;
    }

    private static GenerationOutput? ReadOutput(YamlNode? node, string path, List<SpecificationDiagnostic> errors)
    {
        if (node is null) return null;
        if (node is not YamlMappingNode output || output.Children.Count != 1)
        {
            Error(errors, "INVALID_OUTPUT", "output должен содержать ровно один формат: csv, json или sql.", path, node);
            return null;
        }
        var (formatNode, parametersNode) = output.Children.Single();
        var format = Name(formatNode, path, errors);
        if (format is not ("csv" or "json" or "sql"))
        {
            if (format is not null) Error(errors, "INVALID_OUTPUT", "Неизвестный формат вывода.", path, formatNode);
            return null;
        }
        var formatPath = path + "." + format;
        if (parametersNode is not YamlMappingNode parameters)
        {
            Error(errors, "INVALID_OUTPUT", "Параметры формата должны быть объектом.", formatPath, parametersNode);
            return null;
        }
        if (format == "json")
        {
            CheckKeys(parameters, [], formatPath, errors);
            return new JsonOutput();
        }
        if (format == "csv")
        {
            CheckKeys(parameters, ["delimiter"], formatPath, errors);
            var delimiterNode = Get(parameters, "delimiter");
            if (delimiterNode is null) return new CsvOutput();
            if (delimiterNode is YamlScalarNode { Value.Length: 1 } delimiter && IsString(delimiter) && GenerationOptionsMerger.IsValidDelimiter(delimiter.Value![0]))
                return new CsvOutput(delimiter.Value[0]);
            Error(errors, "INVALID_CSV_DELIMITER", "Разделитель должен быть одним символом, кроме кавычки и символов переноса строки.", formatPath + ".delimiter", delimiterNode);
            return null;
        }

        CheckKeys(parameters, ["dialect", "style"], formatPath, errors);
        var dialectNode = Get(parameters, "dialect");
        if (dialectNode is null)
        {
            Error(errors, "MISSING_SQL_DIALECT", "Укажите диалект SQL.", formatPath + ".dialect", parameters);
            return null;
        }
        var dialectText = Name(dialectNode, formatPath + ".dialect", errors);
        SqlDialect? dialect = dialectText switch { "postgresql" => SqlDialect.PostgreSql, "mysql" => SqlDialect.MySql, "sqlite" => SqlDialect.SQLite, _ => null };
        if (dialectText is not null && dialect is null) Error(errors, "INVALID_SQL_DIALECT", "Допустимые диалекты: postgresql, mysql, sqlite.", formatPath + ".dialect", dialectNode);
        SqlOutputStyle? style = null;
        if (Get(parameters, "style") is { } styleNode)
        {
            var styleText = Name(styleNode, formatPath + ".style", errors);
            style = styleText switch { "script" => SqlOutputStyle.Script, "insert" => SqlOutputStyle.Insert, _ => null };
            if (styleText is not null && style is null) Error(errors, "INVALID_SQL_STYLE", "Допустимые стили: script и insert.", formatPath + ".style", styleNode);
        }
        return dialect is null ? null : new SqlOutput(dialect.Value, style);
    }
}
