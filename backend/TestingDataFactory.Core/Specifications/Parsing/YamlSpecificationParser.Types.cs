using System.Globalization;
using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace TestingDataFactory.Core.Specifications.Parsing;

public sealed partial class YamlSpecificationParser
{
    private Dictionary<string, TypeDefinition> ReadTypes(YamlMappingNode? mapping, List<SpecificationDiagnostic> errors)
    {
        var declarations = new Dictionary<string, YamlNode>(StringComparer.Ordinal);
        var resolved = new Dictionary<string, TypeDefinition>(StringComparer.Ordinal);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var failed = new HashSet<string>(StringComparer.Ordinal);
        if (mapping is null) return resolved;
        foreach (var (key, value) in mapping.Children)
        {
            var name = Name(key, "$.types", errors);
            if (name is null) continue;
            if (FieldTypes.ContainsKey(name))
                Error(errors, "RESERVED_TYPE_NAME", "Имя собственного типа совпадает с базовым типом.", ChildPath("$.types", name), key);
            else declarations.Add(name, value);
        }

        TypeDefinition? Resolve(string name)
        {
            if (resolved.TryGetValue(name, out var result)) return result;
            if (failed.Contains(name) || !declarations.TryGetValue(name, out var node)) return null;
            var path = ChildPath("$.types", name);
            if (!visiting.Add(name))
            {
                Error(errors, "TYPE_CYCLE", "Обнаружен цикл в определениях типов.", path, node);
                return null;
            }
            if (visiting.Count > 64)
            {
                visiting.Remove(name);
                failed.Add(name);
                Error(errors, "TYPE_DEPTH_EXCEEDED", "Превышена допустимая длина цепочки типов.", path, node);
                return null;
            }
            var errorCount = errors.Count;
            TypeDefinition? definition = null;
            if (node is not YamlMappingNode type)
                Error(errors, "TYPE_NOT_MAPPING", "Определение типа должно быть объектом.", path, node);
            else if (Get(type, "enum") is { } enumNode)
            {
                CheckKeys(type, ["enum", "locale"], path, errors);
                var locale = ReadLocale(type, path, errors);
                var enumType = ReadEnum(enumNode, path + ".enum", errors);
                if (enumType is not null) definition = enumType with { Locale = locale };
            }
            else
            {
                var field = ReadField(node, path, errors, Resolve, isType: true);
                if (field is { Type: FieldType.Enum })
                    Error(errors, "UNSUPPORTED_TYPE_ALIAS", "Псевдонимы перечислений пока не поддерживаются.", path, node);
                else if (field is not null) definition = new PrimitiveTypeDefinition(field) { Locale = field.Locale };
            }
            visiting.Remove(name);
            if (errors.Count == errorCount && definition is not null) resolved.Add(name, definition);
            else failed.Add(name);
            return resolved.GetValueOrDefault(name);
        }

        foreach (var name in declarations.Keys) Resolve(name);
        return resolved;
    }

    private EnumTypeDefinition? ReadEnum(YamlNode node, string path, List<SpecificationDiagnostic> errors)
    {
        if (node is YamlSequenceNode sequence)
        {
            var values = ReadStrings(sequence, path, errors);
            if (values.Count == 0) Error(errors, "EMPTY_ENUM", "Перечисление должно содержать значения.", path, node);
            if (values.Distinct(StringComparer.Ordinal).Count() != values.Count)
                Error(errors, "DUPLICATE_ENUM_VALUE", "Значения перечисления должны быть уникальными.", path, node);
            return new ValuesEnumDefinition(Array.AsReadOnly(values.Select(v => new EnumEntry(v, null)).ToArray()));
        }
        if (node is not YamlMappingNode mapping)
        {
            Error(errors, "INVALID_ENUM", "Перечисление должно быть списком или объектом.", path, node);
            return null;
        }

        if (Get(mapping, "dictionary") is YamlScalarNode dictionaryNode && IsString(dictionaryNode))
        {
            CheckKeys(mapping, ["dictionary", "where"], path, errors);
            var name = Name(dictionaryNode, path + ".dictionary", errors);
            if (name is null) return null;
            if (!dictionaryRegistry.TryGetColumns(name, out var columns))
            {
                Error(errors, "UNKNOWN_DICTIONARY", "Неизвестный встроенный справочник.", path + ".dictionary", dictionaryNode);
                return null;
            }
            var whereNode = Get(mapping, "where");
            var columnTypes = columns.ToDictionary(column => column, _ => FieldType.String, StringComparer.Ordinal);
            var where = whereNode is null ? null : ReadExpression(whereNode, columns, path + ".where", errors, booleanResult: true, variableTypes: columnTypes);
            return new DictionaryEnumDefinition(name, Array.AsReadOnly(columns.ToArray()), where);
        }
        if (Get(mapping, "columns") is YamlSequenceNode || Get(mapping, "rows") is YamlSequenceNode)
            return ReadTableEnum(mapping, path, errors);

        var entries = new List<EnumEntry>();
        var positive = false;
        foreach (var (key, value) in mapping.Children)
        {
            var name = Name(key, path, errors);
            if (value is not YamlScalarNode scalar || scalar.Style != ScalarStyle.Plain ||
                !decimal.TryParse(scalar.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var weight) || weight < 0)
            {
                Error(errors, "INVALID_WEIGHT", "Вес должен быть неотрицательным числом.", ChildPath(path, name ?? "?"), value);
                continue;
            }
            if (name is null) continue;
            positive |= weight > 0;
            entries.Add(new(name, weight));
        }
        if (mapping.Children.Count == 0) Error(errors, "EMPTY_ENUM", "Перечисление должно содержать значения.", path, node);
        else if (!positive) Error(errors, "INVALID_WEIGHT", "Хотя бы один вес должен быть положительным.", path, node);
        return new ValuesEnumDefinition(entries.AsReadOnly());
    }

    private static TableEnumDefinition? ReadTableEnum(YamlMappingNode mapping, string path, List<SpecificationDiagnostic> errors)
    {
        CheckKeys(mapping, ["columns", "rows"], path, errors);
        if (Get(mapping, "columns") is not YamlSequenceNode columnsNode || Get(mapping, "rows") is not YamlSequenceNode rowsNode)
        {
            Error(errors, "INVALID_ENUM_TABLE", "Укажите списки columns и rows.", path, mapping);
            return null;
        }
        var columns = ReadStrings(columnsNode, path + ".columns", errors);
        if (columns.Count == 0) Error(errors, "INVALID_ENUM_TABLE", "Список столбцов не может быть пустым.", path + ".columns", columnsNode);
        if (columns.Distinct(StringComparer.Ordinal).Count() != columns.Count)
            Error(errors, "INVALID_ENUM_TABLE", "Имена столбцов должны быть уникальными.", path + ".columns", columnsNode);
        if (rowsNode.Children.Count == 0) Error(errors, "INVALID_ENUM_TABLE", "Список строк не может быть пустым.", path + ".rows", rowsNode);
        var rows = new List<IReadOnlyList<string>>();
        for (var i = 0; i < rowsNode.Children.Count; i++)
        {
            var rowNode = rowsNode.Children[i];
            var rowPath = path + ".rows[" + i + "]";
            if (rowNode is not YamlSequenceNode row)
            {
                Error(errors, "INVALID_ENUM_ROW", "Строка справочника должна быть списком.", rowPath, rowNode);
                continue;
            }
            var values = ReadStrings(row, rowPath, errors);
            if (row.Children.Count != columns.Count)
                Error(errors, "INVALID_ENUM_ROW", "Число значений строки должно совпадать с числом столбцов.", rowPath, row);
            rows.Add(values.AsReadOnly());
        }
        return new(columns.AsReadOnly(), rows.AsReadOnly());
    }

    private static List<string> ReadStrings(YamlSequenceNode sequence, string path, List<SpecificationDiagnostic> errors)
    {
        var result = new List<string>();
        for (var i = 0; i < sequence.Children.Count; i++)
        {
            var value = Name(sequence.Children[i], path + "[" + i + "]", errors);
            if (value is not null) result.Add(value);
        }
        return result;
    }
}
