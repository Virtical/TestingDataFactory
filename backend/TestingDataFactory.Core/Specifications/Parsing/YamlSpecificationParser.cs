using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using TestingDataFactory.Core.Generation;
using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace TestingDataFactory.Core.Specifications.Parsing;

/// <summary>Разбирает таблицы и собственные типы спецификации.</summary>
public sealed partial class YamlSpecificationParser
{
    private readonly ISpecificationDictionaryRegistry dictionaryRegistry;

    public YamlSpecificationParser(ISpecificationDictionaryRegistry? dictionaryRegistry = null) =>
        this.dictionaryRegistry = dictionaryRegistry ?? new DefaultSpecificationDictionaryRegistry();

    private static readonly Dictionary<string, FieldType> FieldTypes = new(StringComparer.Ordinal)
    {
        ["integer"] = FieldType.Integer, ["decimal"] = FieldType.Decimal, ["string"] = FieldType.String,
        ["regex"] = FieldType.Regex, ["boolean"] = FieldType.Boolean, ["date"] = FieldType.Date,
        ["datetime"] = FieldType.DateTime, ["time"] = FieldType.Time, ["uuid"] = FieldType.Uuid
    };

    public SpecificationParseResult Parse(string yaml, SpecificationParsePurpose purpose = SpecificationParsePurpose.Generation)
    {
        ArgumentNullException.ThrowIfNull(yaml);
        if (!Enum.IsDefined(purpose)) throw new ArgumentOutOfRangeException(nameof(purpose));
        var validationOnly = purpose == SpecificationParsePurpose.DataValidation;
        var input = validationOnly ? IgnoredGenerationSanitizer.Sanitize(yaml) : yaml;
        var structureParser = new YamlSpecificationStructureParser();
        var structure = validationOnly ? structureParser.ParseIgnoringGeneration(input) : structureParser.Parse(input);
        if (!structure.IsSuccess) return new(null, structure.Diagnostics);

        var errors = new List<SpecificationDiagnostic>();
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(input));
            var root = (YamlMappingNode)stream.Documents[0].RootNode;
            var defaultLocale = structure.Structure!.Locale;
            if (!SemanticGeneratorCatalog.TryResolveLocale(defaultLocale, out _))
                Error(errors, "UNSUPPORTED_LOCALE", "Локаль не поддерживается библиотекой генерации.", "$.locale", Get(root, "locale") ?? root);

            var types = ReadTypes((YamlMappingNode?)Get(root, "types"), errors);
            var tables = (YamlMappingNode)Get(root, "tables")!;
            if (tables.Children.Count == 0)
            {
                Error(errors, "EMPTY_TABLES", "Спецификация должна содержать хотя бы одну таблицу.", "$.tables", tables);
            }

            var parsedTables = new Dictionary<string, TableDefinition>(StringComparer.Ordinal);
            var references = new Dictionary<(string Table, string Field), YamlMappingNode>();
            foreach (var (tableKey, tableNode) in tables.Children)
            {
                var name = Name(tableKey, "$.tables", errors);
                var tablePath = ChildPath("$.tables", name ?? "?");
                var table = ReadTable(name, tableNode, tablePath, errors, types, references, defaultLocale);
                if (name is not null && table is not null) parsedTables.Add(name, table);
            }

            var (resolvedTables, tableOrder) = ResolveReferences(parsedTables, references, types, errors, defaultLocale);
            if (errors.Count == 0) CheckTableExpressions(resolvedTables, tables, errors);
            var generation = validationOnly ? null : ReadGeneration(Get(root, "generation"), resolvedTables, errors);
            return errors.Count == 0
                ? new(new Specification(structure.Structure!.Version, structure.Structure.Locale, new ReadOnlyDictionary<string, TypeDefinition>(types),
                    new ReadOnlyDictionary<string, TableDefinition>(resolvedTables), tableOrder, generation), errors.AsReadOnly())
                : new(null, errors.AsReadOnly());
        }
        catch (YamlException ex)
        {
            errors.Add(new("INVALID_YAML", "Не удалось разобрать YAML: " + ex.Message, "$", ex.Start.Line, ex.Start.Column));
            return new(null, errors.AsReadOnly());
        }
    }

    private static TableDefinition? ReadTable(string? name, YamlNode node, string path, List<SpecificationDiagnostic> errors, IReadOnlyDictionary<string, TypeDefinition> types, Dictionary<(string Table, string Field), YamlMappingNode> references, string defaultLocale)
    {
        if (node is not YamlMappingNode table)
        {
            Error(errors, "TABLE_NOT_MAPPING", "Таблица должна быть объектом.", path, node);
            return null;
        }
        CheckKeys(table, ["fields", "rules"], path, errors);
        var fieldsNode = Get(table, "fields");
        if (fieldsNode is null)
        {
            Error(errors, "MISSING_FIELDS", "У таблицы должен быть раздел fields.", path + ".fields", node);
            return null;
        }
        if (fieldsNode is not YamlMappingNode fields)
        {
            Error(errors, "FIELDS_NOT_MAPPING", "Раздел fields должен быть объектом.", path + ".fields", fieldsNode);
            return null;
        }
        if (fields.Children.Count == 0)
            Error(errors, "EMPTY_FIELDS", "Таблица должна содержать хотя бы одно поле.", path + ".fields", fields);

        var result = new Dictionary<string, FieldDefinition>(StringComparer.Ordinal);
        foreach (var (fieldKey, fieldNode) in fields.Children)
        {
            var fieldName = Name(fieldKey, path + ".fields", errors);
            var fieldPath = ChildPath(path + ".fields", fieldName ?? "?");
            FieldDefinition? field;
            if (fieldNode is YamlMappingNode mapping && Get(mapping, "ref") is not null)
            {
                field = ReadReferenceField(mapping, fieldPath, errors);
                if (name is not null && fieldName is not null && field?.Reference is not null)
                    references.Add((name, fieldName), mapping);
            }
            else field = ReadField(fieldNode, fieldPath, errors, typeName => types.GetValueOrDefault(typeName));
            if (fieldName is not null && field is not null) result.Add(fieldName, field with { Locale = field.Locale ?? defaultLocale });
        }
        return name is null ? null : new(name, new ReadOnlyDictionary<string, FieldDefinition>(result), Array.Empty<RuleDefinition>());
    }

    private static FieldDefinition? ReadField(YamlNode node, string path, List<SpecificationDiagnostic> errors, Func<string, TypeDefinition?> resolve, bool isType = false, FieldDefinition? referencedType = null)
    {
        if (node is not YamlMappingNode field)
        {
            Error(errors, "FIELD_NOT_MAPPING", "Поле должно быть объектом.", path, node);
            return null;
        }
        var generatorNode = referencedType is null && !isType ? Get(field, "generator") : null;
        var derivedNode = referencedType is null && !isType && generatorNode is null ? Get(field, "derived") : null;
        CheckKeys(field, referencedType is not null
            ? ["ref", "per_parent", "primary", "unique", "pii", "null_rate", "min", "max", "scale", "min_length", "max_length", "pattern", "true_rate", "locale", "distribution"]
            : generatorNode is not null
            ? ["generator", "primary", "unique", "pii", "null_rate", "min_length", "max_length", "locale", "distribution"]
            : isType
            ? ["type", "min", "max", "scale", "min_length", "max_length", "pattern", "true_rate", "locale"]
            : ["type", "derived", "primary", "unique", "pii", "null_rate", "min", "max", "scale", "min_length", "max_length", "pattern", "true_rate", "column", "locale", "distribution"], path, errors);
        string? generator = null;
        if (generatorNode is not null)
        {
            generator = Name(generatorNode, path + ".generator", errors);
            if (generator is not null && !SemanticGeneratorCatalog.Contains(generator))
                Error(errors, "UNKNOWN_GENERATOR", "Неизвестный генератор данных.", path + ".generator", generatorNode);
        }
        var typeNode = Get(field, "type");
        string? typeName = referencedType?.NamedType;
        if (referencedType is null && generatorNode is null && (typeNode is not null || derivedNode is null) &&
            (typeNode is not YamlScalarNode { Value: not null } typeScalar || !IsString(typeScalar)))
        {
            Error(errors, "INVALID_FIELD_TYPE", "Укажите базовый или объявленный тип.", path + ".type", typeNode ?? node);
            return null;
        }
        if (referencedType is null && generatorNode is null && typeNode is not null) typeName = ((YamlScalarNode)typeNode).Value!;

        TypeDefinition? named = null;
        FieldType type;
        if (generatorNode is not null) type = FieldType.String;
        else if (referencedType is null && typeName is null) type = FieldType.Dynamic;
        else if (referencedType is not null)
        {
            type = referencedType.Type;
            if (typeName is not null) named = resolve(typeName);
        }
        else if (!FieldTypes.TryGetValue(typeName!, out type))
        {
            named = resolve(typeName!);
            if (named is null)
            {
                Error(errors, "INVALID_FIELD_TYPE", "Неизвестный или некорректный тип.", path + ".type", typeNode ?? node);
                return null;
            }
            type = named is PrimitiveTypeDefinition primitive ? primitive.Field.Type : FieldType.Enum;
        }
        var inherited = (named as PrimitiveTypeDefinition)?.Field;
        var locale = ReadLocale(field, path, errors) ?? named?.Locale ?? inherited?.Locale;
        var distribution = ReadDistribution(field, path, errors, type, isType, derivedNode is not null);

        var primary = ReadBoolean(field, "primary", path, errors);
        var unique = ReadBoolean(field, "unique", path, errors);
        var pii = ReadBoolean(field, "pii", path, errors);
        var nullRate = ReadNumber(field, "null_rate", path, errors);
        var temporal = type is FieldType.Date or FieldType.DateTime or FieldType.Time;
        var min = temporal ? null : ReadNumber(field, "min", path, errors, type is FieldType.Integer or FieldType.Decimal) ?? inherited?.Min;
        var max = temporal ? null : ReadNumber(field, "max", path, errors, type is FieldType.Integer or FieldType.Decimal) ?? inherited?.Max;
        var temporalMin = temporal ? (Get(field, "min") is null ? inherited?.TemporalMin : ReadTemporal(field, "min", path, type, errors)) : null;
        var temporalMax = temporal ? (Get(field, "max") is null ? inherited?.TemporalMax : ReadTemporal(field, "max", path, type, errors)) : null;
        var scale = ReadNonNegativeInteger(field, "scale", path, errors, type == FieldType.Decimal) ?? inherited?.Scale;
        var minLength = ReadNonNegativeInteger(field, "min_length", path, errors, type == FieldType.String) ?? inherited?.MinLength;
        var maxLength = ReadNonNegativeInteger(field, "max_length", path, errors, type == FieldType.String) ?? inherited?.MaxLength;
        var trueRate = ReadNumber(field, "true_rate", path, errors, type == FieldType.Boolean) ?? inherited?.TrueRate;
        string? pattern = inherited?.Pattern ?? referencedType?.Pattern;
        if (Get(field, "pattern") is { } patternNode)
        {
            if (type != FieldType.Regex) Error(errors, "INCOMPATIBLE_PARAMETER", "Параметр применим только к типу regex.", path + ".pattern", patternNode);
            else if (patternNode is not YamlScalarNode { Value.Length: > 0 } scalar || !IsString(scalar))
                Error(errors, "INVALID_PARAMETER", "Шаблон должен быть непустой строкой.", path + ".pattern", patternNode);
            else
            {
                pattern = scalar.Value;
                try { _ = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)); }
                catch (ArgumentException) { Error(errors, "INVALID_REGEX", "Некорректное регулярное выражение .NET.", path + ".pattern", patternNode); }
            }
        }
        else if (type == FieldType.Regex && pattern is null) Error(errors, "MISSING_PATTERN", "Для regex необходим pattern.", path + ".pattern", node);

        var columnNode = Get(field, "column");
        string? column = referencedType?.Column;
        IReadOnlyList<string>? columns = named switch
        {
            TableEnumDefinition table => table.Columns,
            DictionaryEnumDefinition dictionary => dictionary.Columns,
            _ => null
        };
        if (columnNode is not null)
        {
            column = Name(columnNode, path + ".column", errors);
            if (columns is null || columns.Count == 0)
                Error(errors, "INVALID_COLUMN", "column допустим только для многостолбцового перечисления или справочника.", path + ".column", columnNode);
            else if (column is not null && !columns.Contains(column, StringComparer.Ordinal))
                Error(errors, "INVALID_COLUMN", "Столбец отсутствует в типе.", path + ".column", columnNode);
        }
        else if (!isType && column is null && columns is { Count: > 0 })
            Error(errors, "MISSING_COLUMN", "Для многостолбцового типа укажите column.", path + ".column", node);

        if (min > max) Error(errors, "INVALID_RANGE", "min не может быть больше max.", path, node);
        if (temporal) CheckTemporalRange(temporalMin, temporalMax, path, node, errors);
        if (minLength > maxLength) Error(errors, "INVALID_RANGE", "min_length не может быть больше max_length.", path, node);
        if (nullRate is < 0 or > 1) Error(errors, "INVALID_RATE", "null_rate должен быть от 0 до 1.", path + ".null_rate", Get(field, "null_rate")!);
        if (trueRate is < 0 or > 1) Error(errors, "INVALID_RATE", "true_rate должен быть от 0 до 1.", path + ".true_rate", Get(field, "true_rate")!);
        if (primary && nullRate > 0) Error(errors, "PRIMARY_NULLABLE", "Первичный ключ не может допускать NULL.", path + ".null_rate", Get(field, "null_rate")!);
        if (type == FieldType.Integer && ((min is not null && decimal.Truncate(min.Value) != min) || (max is not null && decimal.Truncate(max.Value) != max)))
            Error(errors, "INVALID_INTEGER_RANGE", "Границы integer должны быть целыми числами.", path, node);

        var derivedSource = derivedNode is null ? null : Name(derivedNode, path + ".derived", errors);
        return new(type, primary, primary || unique, pii, nullRate, min, max, scale, minLength, maxLength, pattern, trueRate,
            named is null ? null : typeName, column, TemporalMin: temporalMin, TemporalMax: temporalMax, Generator: generator, Locale: locale,
            Derived: derivedSource is null ? null : new CelExpression(derivedSource, Array.Empty<string>()), Distribution: distribution);
    }

    private static void CheckKeys(YamlMappingNode mapping, string[] allowed, string path, List<SpecificationDiagnostic> errors)
    {
        foreach (var key in mapping.Children.Keys)
        {
            var name = Name(key, path, errors);
            if (name is not null && !allowed.Contains(name, StringComparer.Ordinal))
                Error(errors, "UNSUPPORTED_PARAMETER", "Неизвестный или пока не поддерживаемый параметр.", ChildPath(path, name), key);
        }
    }

    private static string? Name(YamlNode node, string path, List<SpecificationDiagnostic> errors)
    {
        if (node is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value) && IsString(scalar)) return scalar.Value;
        Error(errors, "INVALID_NAME", "Имя должно быть непустой строкой.", path, node);
        return null;
    }

    private static string? ReadLocale(YamlMappingNode mapping, string path, List<SpecificationDiagnostic> errors)
    {
        var node = Get(mapping, "locale");
        if (node is null) return null;
        var locale = Name(node, path + ".locale", errors);
        if (locale is not null && !SemanticGeneratorCatalog.TryResolveLocale(locale, out _))
            Error(errors, "UNSUPPORTED_LOCALE", "Локаль не поддерживается библиотекой генерации.", path + ".locale", node);
        return locale;
    }

    private static bool ReadBoolean(YamlMappingNode field, string name, string path, List<SpecificationDiagnostic> errors)
    {
        var node = Get(field, name);
        if (node is null) return false;
        if (node is YamlScalarNode scalar && scalar.Style == ScalarStyle.Plain && bool.TryParse(scalar.Value, out var value)) return value;
        Error(errors, "INVALID_PARAMETER", "Ожидается логическое значение true или false.", path + "." + name, node);
        return false;
    }

    private static decimal? ReadNumber(YamlMappingNode field, string name, string path, List<SpecificationDiagnostic> errors, bool supported = true)
    {
        var node = Get(field, name);
        if (node is null) return null;
        if (!supported)
        {
            Error(errors, "INCOMPATIBLE_PARAMETER", "Параметр пока не поддерживается для этого типа.", path + "." + name, node);
            return null;
        }
        if (node is YamlScalarNode scalar && scalar.Style == ScalarStyle.Plain &&
            decimal.TryParse(scalar.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return value;
        Error(errors, "INVALID_PARAMETER", "Ожидается число.", path + "." + name, node);
        return null;
    }

    private static int? ReadNonNegativeInteger(YamlMappingNode field, string name, string path, List<SpecificationDiagnostic> errors, bool supported)
    {
        var number = ReadNumber(field, name, path, errors, supported);
        if (number is null) return null;
        if (number < 0 || number > int.MaxValue || decimal.Truncate(number.Value) != number)
        {
            Error(errors, "INVALID_PARAMETER", "Ожидается неотрицательное целое число.", path + "." + name, Get(field, name)!);
            return null;
        }
        return (int)number.Value;
    }

    private static YamlNode? Get(YamlMappingNode mapping, string key) => mapping.Children.TryGetValue(new YamlScalarNode(key), out var node) ? node : null;
    private static bool IsNull(YamlScalarNode node) => node.Style == ScalarStyle.Plain && (node.Value is null or "" or "~" or "null" or "Null" or "NULL");
    private static bool IsString(YamlScalarNode node) => !IsNull(node) &&
        (node.Style != ScalarStyle.Plain ||
         (!bool.TryParse(node.Value, out _) && !decimal.TryParse(node.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)));
    private static string ChildPath(string path, string name) => path + "['" + name.Replace("'", "\\'") + "']";
    private static void Error(List<SpecificationDiagnostic> errors, string code, string message, string path, YamlNode node) =>
        errors.Add(new(code, message, path, node.Start.Line, node.Start.Column));
}
