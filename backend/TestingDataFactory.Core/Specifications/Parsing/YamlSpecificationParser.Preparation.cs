using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;
using YamlDotNet.RepresentationModel;

namespace TestingDataFactory.Core.Specifications.Parsing;

internal sealed partial class YamlSpecificationParser
{
    internal ParseResult ParseForValidation(string yaml)
    {
        var preparation = Prepare(yaml);
        if (preparation.Specification is not { } prepared) return new(null, preparation.Diagnostics);
        var errors = new List<SpecificationDiagnostic>(preparation.Diagnostics);
        CheckExpressionSyntax(prepared, errors);
        var generation = ReadGeneration(prepared.GenerationNode, prepared.Tables, errors, validateReferences: false);
        return errors.Count == 0
            ? new(new ParsedSpecification(prepared, generation), errors.AsReadOnly())
            : new(null, errors.AsReadOnly());
    }

    private static void CheckExpressionSyntax(PreparedSpecification prepared, List<SpecificationDiagnostic> errors)
    {
        foreach (var (name, table) in prepared.Tables)
        {
            var path = ChildPath("$.tables", name);
            var source = (YamlMappingNode)Get(prepared.TableNodes, name)!;
            var fieldNodes = (YamlMappingNode)Get(source, "fields")!;
            foreach (var (fieldName, field) in table.Fields)
            {
                if (field.Derived is null || Get(fieldNodes, fieldName) is not YamlMappingNode mapping || Get(mapping, "derived") is not { } node) continue;
                var error = CelExpressionValidator.ValidateSyntax(field.Derived.Source);
                if (error is not null) Error(errors, "INVALID_CEL", error, ChildPath(path + ".fields", fieldName) + ".derived", node);
            }
            if (Get(source, "rules") is not { } rulesNode) continue;
            if (rulesNode is not YamlSequenceNode rules)
            {
                Error(errors, "INVALID_RULES", "Раздел rules должен быть списком.", path + ".rules", rulesNode);
                continue;
            }
            for (var i = 0; i < rules.Children.Count; i++)
            {
                var rulePath = path + ".rules[" + i + "]";
                if (rules.Children[i] is not YamlMappingNode rule)
                {
                    Error(errors, "INVALID_RULE", "Правило должно быть объектом.", rulePath, rules.Children[i]);
                    continue;
                }
                CheckKeys(rule, ["expr", "when", "message", "severity"], rulePath, errors);
                if (Get(rule, "expr") is null) Error(errors, "MISSING_RULE_EXPRESSION", "Укажите expr.", rulePath + ".expr", rule);
                foreach (var key in new[] { "expr", "when" })
                {
                    if (Get(rule, key) is not { } node) continue;
                    var text = Name(node, rulePath + "." + key, errors);
                    if (text is null) continue;
                    var error = CelExpressionValidator.ValidateSyntax(text);
                    if (error is not null) Error(errors, "INVALID_CEL", error, rulePath + "." + key, node);
                }
                if (Get(rule, "message") is { } message) _ = Name(message, rulePath + ".message", errors);
                if (Get(rule, "severity") is { } severity)
                {
                    var value = Name(severity, rulePath + ".severity", errors);
                    if (value is not null and not ("error" or "warning")) Error(errors, "INVALID_SEVERITY", "Допустимые значения: error и warning.", rulePath + ".severity", severity);
                }
            }
        }
    }
}
