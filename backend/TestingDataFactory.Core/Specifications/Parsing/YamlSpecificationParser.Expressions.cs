using System.Collections.ObjectModel;
using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;
using YamlDotNet.RepresentationModel;

namespace TestingDataFactory.Core.Specifications.Parsing;

public sealed partial class YamlSpecificationParser
{
    private static void CheckTableExpressions(Dictionary<string, TableDefinition> tables, YamlMappingNode tableNodes,
        List<SpecificationDiagnostic> errors)
    {
        foreach (var (name, table) in tables.ToArray())
        {
            var source = (YamlMappingNode)Get(tableNodes, name)!;
            var fieldNodes = (YamlMappingNode)Get(source, "fields")!;
            var fields = new Dictionary<string, FieldDefinition>(table.Fields, StringComparer.Ordinal);
            var path = ChildPath("$.tables", name);
            CheckDerivedFields(fields, fieldNodes, path, errors);
            var rules = ReadRules(Get(source, "rules"), fields, path + ".rules", errors);
            tables[name] = new TableDefinition(name, new ReadOnlyDictionary<string, FieldDefinition>(fields), rules);
        }
    }

    private static IReadOnlyList<RuleDefinition> ReadRules(YamlNode? node, IReadOnlyDictionary<string, FieldDefinition> fields, string path, List<SpecificationDiagnostic> errors)
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
            var expr = ReadExpression(exprNode, fields.Keys, rulePath + ".expr", errors, booleanResult: true, variableTypes: fieldTypes);
            var whenNode = Get(rule, "when");
            var when = whenNode is null ? null : ReadExpression(whenNode, fields.Keys, rulePath + ".when", errors, booleanResult: true, variableTypes: fieldTypes);
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

    private static void CheckDerivedFields(Dictionary<string, FieldDefinition> fields, YamlMappingNode nodes, string tablePath, List<SpecificationDiagnostic> errors)
    {
        var fieldTypes = fields.ToDictionary(pair => pair.Key, pair => pair.Value.Type, StringComparer.Ordinal);
        foreach (var (name, field) in fields.ToArray())
        {
            if (field.Derived is null) continue;
            var fieldNode = Get(nodes, name) as YamlMappingNode;
            var node = fieldNode is null ? null : Get(fieldNode, "derived");
            if (node is null) continue;
            var expression = ReadExpression(node, fields.Keys, ChildPath(tablePath + ".fields", name) + ".derived", errors, expectedType: field.Type, variableTypes: fieldTypes);
            if (expression is not null) fields[name] = field with { Derived = expression };
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
        IReadOnlyDictionary<string, FieldType>? variableTypes = null)
    {
        var source = Name(node, path, errors);
        if (source is null) return null;
        var (expression, error) = CelExpressionValidator.Validate(source, variables, booleanResult, expectedType, variableTypes);
        if (error is not null) Error(errors, "INVALID_CEL", error, path, node);
        return expression;
    }
}
