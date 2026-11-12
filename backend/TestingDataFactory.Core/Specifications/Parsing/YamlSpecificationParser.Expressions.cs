using System.Collections.ObjectModel;
using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;
using YamlDotNet.RepresentationModel;

namespace TestingDataFactory.Core.Specifications.Parsing;

internal sealed partial class YamlSpecificationParser
{
    private static void CheckTableExpressions(IReadOnlyDictionary<string, TableDefinition> parsedTables,
        Dictionary<string, TableDefinition> tables, YamlMappingNode tableNodes,
        List<SpecificationDiagnostic> errors)
    {
        // Snapshot field errors before expression checking, so later diagnostics do not change
        // which fields are available to other independent expressions.
        var invalidFields = new HashSet<(string Table, string Field)>();
        foreach (var (name, table) in parsedTables)
        foreach (var fieldName in table.Fields.Keys)
        {
            var fieldPath = ChildPath(ChildPath("$.tables", name) + ".fields", fieldName);
            if (errors.Any(d => d.Path == fieldPath || d.Path.StartsWith(fieldPath + ".", StringComparison.Ordinal) ||
                                d.Path.StartsWith(fieldPath + "[", StringComparison.Ordinal)))
                invalidFields.Add((name, fieldName));
        }
        bool changed;
        do
        {
            changed = false;
            foreach (var (name, table) in parsedTables)
            foreach (var (fieldName, field) in table.Fields)
                if (field.Reference is { } reference && invalidFields.Contains((reference.Table, reference.Field)))
                    changed |= invalidFields.Add((name, fieldName));
        } while (changed);

        foreach (var (name, parsedTable) in parsedTables)
        {
            var source = (YamlMappingNode)Get(tableNodes, name)!;
            var fieldNodes = (YamlMappingNode)Get(source, "fields")!;
            var table = tables.GetValueOrDefault(name);
            var fields = table is null
                ? parsedTable.Fields.Where(pair => pair.Value.Reference is null).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : new Dictionary<string, FieldDefinition>(table.Fields, StringComparer.Ordinal);
            var unavailable = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in fieldNodes.Children.Keys.OfType<YamlScalarNode>())
            {
                if (key.Value is not { Length: > 0 } fieldName) continue;
                if (!fields.ContainsKey(fieldName) || invalidFields.Contains((name, fieldName)))
                {
                    unavailable.Add(fieldName);
                    fields.Remove(fieldName);
                }
            }
            var path = ChildPath("$.tables", name);
            CheckDerivedFields(fields, parsedTable.Fields, fieldNodes, path, errors, unavailable);
            var rules = ReadRules(Get(source, "rules"), fields, path + ".rules", errors, unavailable);
            if (table is not null && unavailable.Count == 0)
                tables[name] = new TableDefinition(name, new ReadOnlyDictionary<string, FieldDefinition>(fields), rules);
        }
    }

    private static IReadOnlyList<RuleDefinition> ReadRules(YamlNode? node, IReadOnlyDictionary<string, FieldDefinition> fields, string path, List<SpecificationDiagnostic> errors, IReadOnlySet<string> unavailable)
    {
        var result = new List<RuleDefinition>();
        if (node is null) return result.AsReadOnly();
        if (node is not YamlSequenceNode rules)
        {
            Error(errors, "INVALID_RULES", "Раздел rules должен быть списком.", path, node);
            return result.AsReadOnly();
        }
        var fieldTypes = fields.ToDictionary(pair => pair.Key, pair => pair.Value.Type, StringComparer.Ordinal);
        for (var i = 0; i < rules.Children.Count; i++)
        {
            var rulePath = path + "[" + i + "]";
            var entry = rules.Children[i];
            if (entry is not YamlMappingNode rule)
            {
                Error(errors, "INVALID_RULE", "Правило должно быть объектом.", rulePath, entry);
                continue;
            }
            CheckKeys(rule, ["expr", "when", "message", "severity"], rulePath, errors);
            var exprNode = Get(rule, "expr");
            if (exprNode is null)
            {
                Error(errors, "MISSING_RULE_EXPRESSION", "Укажите expr.", rulePath + ".expr", rule);
                continue;
            }
            var expr = ReadExpression(exprNode, fields.Keys, rulePath + ".expr", errors, booleanResult: true, variableTypes: fieldTypes, fields: fields, unavailable: unavailable);
            var whenNode = Get(rule, "when");
            var when = whenNode is null ? null : ReadExpression(whenNode, fields.Keys, rulePath + ".when", errors, booleanResult: true, variableTypes: fieldTypes, fields: fields, unavailable: unavailable);
            string? message = null;
            if (Get(rule, "message") is { } messageNode) message = Name(messageNode, rulePath + ".message", errors);
            var severity = RuleSeverity.Error;
            if (Get(rule, "severity") is { } severityNode)
            {
                var value = Name(severityNode, rulePath + ".severity", errors);
                if (value == "warning") severity = RuleSeverity.Warning;
                else if (value is not null && value != "error") Error(errors, "INVALID_SEVERITY", "Допустимые значения: error и warning.", rulePath + ".severity", severityNode);
            }
            if (expr is not null && (whenNode is null || when is not null)) result.Add(new(expr, when, message, severity));
        }
        return result.AsReadOnly();
    }

    private static void CheckDerivedFields(Dictionary<string, FieldDefinition> fields, IReadOnlyDictionary<string, FieldDefinition> parsedFields,
        YamlMappingNode nodes, string tablePath, List<SpecificationDiagnostic> errors, IReadOnlySet<string> unavailable)
    {
        var fieldTypes = fields.ToDictionary(pair => pair.Key, pair => pair.Value.Type, StringComparer.Ordinal);
        foreach (var (name, field) in parsedFields)
        {
            if (field.Derived is null) continue;
            var fieldNode = Get(nodes, name) as YamlMappingNode;
            var node = fieldNode is null ? null : Get(fieldNode, "derived");
            if (node is null) continue;
            var expression = ReadExpression(node, fields.Keys, ChildPath(tablePath + ".fields", name) + ".derived", errors,
                expectedType: unavailable.Contains(name) ? null : field.Type, variableTypes: fieldTypes, fields: fields, unavailable: unavailable);
            if (expression is not null && fields.TryGetValue(name, out var resolvedField)) fields[name] = resolvedField with { Derived = expression };
        }

        // A malformed type may prevent the field from being constructed at all. Its
        // expression can still have independent syntax, name or operation errors.
        foreach (var (key, value) in nodes.Children)
        {
            if (key is not YamlScalarNode { Value: { Length: > 0 } name } || parsedFields.ContainsKey(name) ||
                value is not YamlMappingNode mapping || Get(mapping, "derived") is not { } expressionNode) continue;
            _ = ReadExpression(expressionNode, fields.Keys, ChildPath(tablePath + ".fields", name) + ".derived", errors,
                variableTypes: fieldTypes, fields: fields, unavailable: unavailable);
        }

        var derived = fields.Where(pair => pair.Value.Derived is not null).ToDictionary(pair => pair.Key, pair => pair.Value.Derived!, StringComparer.Ordinal);
        var indegree = derived.Keys.ToDictionary(name => name, _ => 0, StringComparer.Ordinal);
        var outgoing = derived.Keys.ToDictionary(name => name, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var (name, expression) in derived)
        foreach (var dependency in expression.Dependencies.Where(derived.ContainsKey).Distinct(StringComparer.Ordinal))
        {
            indegree[name]++;
            outgoing[dependency].Add(name);
        }
        var queue = new Queue<string>(indegree.Where(pair => pair.Value == 0).Select(pair => pair.Key));
        var count = 0;
        while (queue.TryDequeue(out var name))
        {
            count++;
            foreach (var dependent in outgoing[name]) if (--indegree[dependent] == 0) queue.Enqueue(dependent);
        }
        if (count != derived.Count)
            errors.Add(new("DERIVED_CYCLE", "Обнаружен цикл вычисляемых полей.", tablePath + ".fields"));
    }

    private static CelExpression? ReadExpression(YamlNode node, IEnumerable<string> variables, string path,
        List<SpecificationDiagnostic> errors, bool booleanResult = false, FieldType? expectedType = null,
        IReadOnlyDictionary<string, FieldType>? variableTypes = null, IReadOnlyDictionary<string, FieldDefinition>? fields = null,
        IReadOnlySet<string>? unavailable = null)
    {
        var source = Name(node, path, errors);
        if (source is null) return null;
        if (unavailable is { Count: > 0 } && CelExpressionValidator.ReferencesAny(source, unavailable)) return null;
        var (expression, error) = CelExpressionValidator.Validate(source, variables, booleanResult, expectedType, variableTypes, fields);
        if (error is not null) Error(errors, "INVALID_CEL", error, path, node);
        return expression;
    }
}
