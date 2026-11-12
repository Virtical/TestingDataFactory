using System.Collections.ObjectModel;
using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;
using YamlDotNet.RepresentationModel;

namespace TestingDataFactory.Core.Specifications.Parsing;

internal sealed partial class YamlSpecificationParser
{
    private static FieldDefinition? ReadReferenceField(YamlMappingNode field, string path, List<SpecificationDiagnostic> errors)
    {
        CheckKeys(field, ["ref", "per_parent", "primary", "unique", "pii", "null_rate", "min", "max", "scale", "min_length", "max_length", "pattern", "true_rate", "locale", "distribution"], path, errors);
        var refNode = Get(field, "ref")!;
        var text = refNode is YamlScalarNode scalar && IsString(scalar) ? scalar.Value : null;
        var parts = text?.Split('.');
        if (parts is not { Length: 2 } || parts.Any(p => string.IsNullOrWhiteSpace(p) || p != p.Trim()))
        {
            Error(errors, "INVALID_REFERENCE", "Ссылка должна иметь вид таблица.поле.", path + ".ref", refNode);
            return null;
        }

        PerParentRange? perParent = null;
        if (Get(field, "per_parent") is { } parentNode)
        {
            if (parentNode is not YamlMappingNode parent)
                Error(errors, "INVALID_PER_PARENT", "per_parent должен быть объектом с min и max.", path + ".per_parent", parentNode);
            else
            {
                var parentPath = path + ".per_parent";
                CheckKeys(parent, ["min", "max"], parentPath, errors);
                if (Get(parent, "min") is null) Error(errors, "MISSING_PER_PARENT_BOUND", "Укажите min.", parentPath + ".min", parent);
                if (Get(parent, "max") is null) Error(errors, "MISSING_PER_PARENT_BOUND", "Укажите max.", parentPath + ".max", parent);
                var min = ReadNonNegativeInteger(parent, "min", parentPath, errors, true);
                var max = ReadNonNegativeInteger(parent, "max", parentPath, errors, true);
                if (min > max) Error(errors, "INVALID_RANGE", "min не может быть больше max.", parentPath, parent);
                if (min is not null && max is not null) perParent = new(min.Value, max.Value);
            }
        }

        return new(FieldType.Reference, false, false, false, null, null, null, null, null, null, null, null,
            Reference: new FieldReference(parts[0], parts[1], perParent));
    }

    private static (Dictionary<string, TableDefinition> Tables, IReadOnlyList<string> Order) ResolveReferences(
        IReadOnlyDictionary<string, TableDefinition> tables,
        IReadOnlyDictionary<(string Table, string Field), YamlMappingNode> locations,
        IReadOnlyDictionary<string, TypeDefinition> types,
        List<SpecificationDiagnostic> errors, string defaultLocale)
    {
        var dependents = tables.Keys.ToDictionary(name => name, _ => new List<string>(), StringComparer.Ordinal);
        var edges = new HashSet<(string Parent, string Child)>();
        var indegree = tables.Keys.ToDictionary(name => name, _ => 0, StringComparer.Ordinal);
        foreach (var (tableName, table) in tables)
        foreach (var (fieldName, field) in table.Fields)
        {
            if (field.Reference is not { } reference) continue;
            var path = ChildPath(ChildPath("$.tables", tableName) + ".fields", fieldName) + ".ref";
            var location = Get(locations[(tableName, fieldName)], "ref")!;
            if (!tables.TryGetValue(reference.Table, out var targetTable))
            {
                Error(errors, "UNKNOWN_REFERENCE_TABLE", "Указанная таблица не найдена.", path, location);
                continue;
            }
            if (!targetTable.Fields.ContainsKey(reference.Field))
            {
                Error(errors, "UNKNOWN_REFERENCE_FIELD", "Указанное поле не найдено в целевой таблице.", path, location);
                continue;
            }
            if (edges.Add((reference.Table, tableName)))
            {
                dependents[reference.Table].Add(tableName);
                indegree[tableName]++;
            }
        }

        var ready = new Queue<string>(tables.Keys.Where(name => indegree[name] == 0));
        var order = new List<string>();
        while (ready.TryDequeue(out var parent))
        {
            order.Add(parent);
            foreach (var child in dependents[parent])
                if (--indegree[child] == 0) ready.Enqueue(child);
        }
        if (order.Count != tables.Count)
            errors.Add(new("REFERENCE_CYCLE", "Обнаружен цикл ссылок между таблицами.", "$.tables"));

        var resolved = new Dictionary<string, TableDefinition>(StringComparer.Ordinal);
        foreach (var name in order)
        {
            var table = tables[name];
            var fields = new Dictionary<string, FieldDefinition>(StringComparer.Ordinal);
            foreach (var (fieldName, field) in table.Fields)
            {
                if (field.Reference is { } reference)
                {
                    if (!resolved.TryGetValue(reference.Table, out var targetTable) || !targetTable.Fields.TryGetValue(reference.Field, out var target)) continue;
                    var path = ChildPath(ChildPath("$.tables", name) + ".fields", fieldName);
                    var source = locations[(name, fieldName)];
                    var checkedField = ReadField(source, path, errors, typeName => types.GetValueOrDefault(typeName), referencedType: target);
                    if (checkedField is not null)
                    {
                        CheckReferenceCompatibility(checkedField, target, path, source, errors);
                        fields.Add(fieldName, ApplyLocale(checkedField with { Reference = reference }, defaultLocale));
                    }
                }
                else fields.Add(fieldName, field);
            }
            resolved.Add(name, new(table.Name, new ReadOnlyDictionary<string, FieldDefinition>(fields), table.Rules));
        }
        return (resolved, order.AsReadOnly());
    }

    private static void CheckReferenceCompatibility(FieldDefinition field, FieldDefinition target, string path,
        YamlNode node, List<SpecificationDiagnostic> errors)
    {
        if (field.Min > target.Max || field.Max < target.Min ||
            field.MinLength > target.MaxLength || field.MaxLength < target.MinLength)
            Error(errors, "INCOMPATIBLE_REFERENCE_RANGE", "Ограничения поля и цели ссылки не пересекаются.", path, node);
        CheckTemporalRange(target.TemporalMin, field.TemporalMax, path, node, errors);
        CheckTemporalRange(field.TemporalMin, target.TemporalMax, path, node, errors);
    }
}
