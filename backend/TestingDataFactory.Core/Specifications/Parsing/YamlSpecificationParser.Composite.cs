using System.Collections.ObjectModel;
using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;
using YamlDotNet.RepresentationModel;

namespace TestingDataFactory.Core.Specifications.Parsing;

internal sealed partial class YamlSpecificationParser
{
    private static (IReadOnlyDictionary<string, FieldDefinition>? Fields, FieldDefinition? Items, int? MinItems, int? MaxItems)
        ReadComposite(YamlMappingNode node, string path, List<SpecificationDiagnostic> errors,
            Func<string, TypeDefinition?> resolve, FieldType type, FieldDefinition? inherited)
    {
        if (type is FieldType.Object or FieldType.Array)
        {
            foreach (var key in new[] { "ref", "derived", "primary", "unique" })
            {
                var parameterPath = path + "." + key;
                if (Get(node, key) is { } unsupported && !errors.Any(d => d.Code == "UNSUPPORTED_NESTED_PARAMETER" && d.Path == parameterPath))
                    Error(errors, "UNSUPPORTED_COMPOSITE_PARAMETER", "Параметр не поддерживается для object или array.", parameterPath, unsupported);
            }
        }
        var fieldsNode = Get(node, "fields");
        var itemsNode = Get(node, "items");
        IReadOnlyDictionary<string, FieldDefinition>? fields = inherited?.Fields;
        var items = inherited?.Items;
        if (fieldsNode is not null)
        {
            if (type != FieldType.Object)
                Error(errors, "INCOMPATIBLE_PARAMETER", "fields применим только к object.", path + ".fields", fieldsNode);
            else if (fields is not null)
                Error(errors, "UNSUPPORTED_PARAMETER", "Нельзя переопределить fields именованного типа.", path + ".fields", fieldsNode);
            else if (fieldsNode is not YamlMappingNode mapping)
                Error(errors, "FIELDS_NOT_MAPPING", "Раздел fields должен быть объектом.", path + ".fields", fieldsNode);
            else
            {
                if (mapping.Children.Count == 0)
                    Error(errors, "EMPTY_FIELDS", "Объект должен содержать хотя бы одно поле.", path + ".fields", fieldsNode);
                var result = new Dictionary<string, FieldDefinition>(StringComparer.Ordinal);
                foreach (var (key, value) in mapping.Children)
                {
                    var name = Name(key, path + ".fields", errors);
                    var fieldPath = ChildPath(path + ".fields", name ?? "?");
                    var child = ReadNestedField(value, fieldPath, errors, resolve);
                    if (name is not null && child is not null) result.Add(name, child);
                }
                fields = new ReadOnlyDictionary<string, FieldDefinition>(result);
            }
        }
        else if (type == FieldType.Object && fields is null)
            Error(errors, "MISSING_FIELDS", "Для object укажите fields.", path + ".fields", node);

        if (itemsNode is not null)
        {
            if (type != FieldType.Array)
                Error(errors, "INCOMPATIBLE_PARAMETER", "items применим только к array.", path + ".items", itemsNode);
            else if (items is not null)
                Error(errors, "UNSUPPORTED_PARAMETER", "Нельзя переопределить items именованного типа.", path + ".items", itemsNode);
            else items = ReadNestedField(itemsNode, path + ".items", errors, resolve);
        }
        else if (type == FieldType.Array && items is null)
            Error(errors, "MISSING_ITEMS", "Для array укажите items.", path + ".items", node);

        var min = ReadNonNegativeInteger(node, "min_items", path, errors, type == FieldType.Array) ?? inherited?.MinItems;
        var max = ReadNonNegativeInteger(node, "max_items", path, errors, type == FieldType.Array) ?? inherited?.MaxItems;
        if (min > max) Error(errors, "INVALID_RANGE", "min_items не может быть больше max_items.", path, node);
        return (fields, items, min, max);
    }

    private static FieldDefinition? ReadNestedField(YamlNode node, string path, List<SpecificationDiagnostic> errors,
        Func<string, TypeDefinition?> resolve)
    {
        if (node is YamlMappingNode mapping)
        {
            foreach (var key in new[] { "ref", "derived", "primary", "unique" })
                if (Get(mapping, key) is { } unsupported)
                    Error(errors, "UNSUPPORTED_NESTED_PARAMETER", "Параметр пока не поддерживается внутри object или array.", path + "." + key, unsupported);
        }
        return ReadField(node, path, errors, resolve);
    }

    private static FieldDefinition ApplyLocale(FieldDefinition field, string parentLocale)
    {
        var locale = field.Locale ?? parentLocale;
        var fields = field.Fields is null ? null : new ReadOnlyDictionary<string, FieldDefinition>(
            field.Fields.ToDictionary(pair => pair.Key, pair => ApplyLocale(pair.Value, locale), StringComparer.Ordinal));
        return field with { Locale = locale, Fields = fields, Items = field.Items is null ? null : ApplyLocale(field.Items, locale) };
    }
}
