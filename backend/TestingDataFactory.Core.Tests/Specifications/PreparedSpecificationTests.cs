using NUnit.Framework;
using TestingDataFactory.Core.Specifications.Model;
using TestingDataFactory.Core.Specifications.Parsing;
using YamlDotNet.RepresentationModel;

namespace TestingDataFactory.Core.Tests.Specifications;

public class PreparedSpecificationTests
{
    private readonly YamlSpecificationParser parser = new();

    [Test]
    public void KeepsReferencesAndTablesWithCyclesBeforeResolution()
    {
        const string yaml = """
            spec_version: '1.0'
            tables:
              first:
                fields:
                  id: { type: integer }
                  second_id: { ref: second.id, per_parent: { min: 1, max: 2 } }
              second:
                fields:
                  id: { type: integer }
                  first_id: { ref: first.id }
            """;
        var preparation = parser.Prepare(yaml);
        Assert.That(preparation.Diagnostics, Is.Empty);
        var prepared = preparation.Specification!;
        Assert.That(prepared.Tables.Keys, Is.EquivalentTo(new[] { "first", "second" }));
        var field = prepared.Tables["first"].Fields["second_id"];
        Assert.That(field.Type, Is.EqualTo(FieldType.Reference));
        Assert.That(field.Reference, Is.EqualTo(new FieldReference("second", "id", new PerParentRange(1, 2))));
        Assert.That(prepared.Tables["second"].Fields["first_id"].Reference!.Table, Is.EqualTo("first"));
        Assert.That(prepared.ReferenceNodes[("first", "second_id")].Start.Line, Is.GreaterThan(0));
        Assert.That(parser.Parse(yaml).Diagnostics.Select(d => d.Code), Does.Contain("REFERENCE_CYCLE"));
    }

    [Test]
    public void KeepsUnresolvedTargetAndSourceForLaterValidation()
    {
        const string yaml = """
            spec_version: '1.0'
            tables:
              child:
                fields:
                  parent_id: { ref: missing.id, unique: true }
            """;
        var preparation = parser.Prepare(yaml);
        Assert.That(preparation.Diagnostics, Is.Empty);
        var prepared = preparation.Specification!;
        Assert.That(prepared.Tables["child"].Fields["parent_id"].Reference!.Table, Is.EqualTo("missing"));
        Assert.That(prepared.ReferenceNodes[("child", "parent_id")].Children.ContainsKey(new YamlScalarNode("unique")), Is.True);
        Assert.That(parser.Parse(yaml).Diagnostics.Select(d => d.Code), Does.Contain("UNKNOWN_REFERENCE_TABLE"));
    }

    [Test]
    public void RetainsExpressionsRulesAndGenerationSource()
    {
        const string yaml = """
            spec_version: '1.0'
            tables:
              t:
                fields:
                  x: { type: integer }
                  y: { type: integer, derived: x + 1 }
                rules:
                  - expr: y > x
            generation: { rows: { t: 5 } }
            """;
        var preparation = parser.Prepare(yaml);
        Assert.That(preparation.Diagnostics, Is.Empty);
        var prepared = preparation.Specification!;
        Assert.That(prepared.Tables["t"].Fields["y"].Derived!.Source, Is.EqualTo("x + 1"));
        var table = (YamlMappingNode)prepared.TableNodes.Children[new YamlScalarNode("t")];
        Assert.That(table.Children[new YamlScalarNode("rules")], Is.TypeOf<YamlSequenceNode>());
        Assert.That(prepared.GenerationNode, Is.TypeOf<YamlMappingNode>());
    }

    [Test]
    public void ReturnsStructuralDiagnosticsWithoutPreparedSpecification()
    {
        var preparation = parser.Prepare("spec_version: '2.0'\ntables: {}\n");
        Assert.That(preparation.Specification, Is.Null);
        Assert.That(preparation.Diagnostics.Select(d => d.Code), Does.Contain("UNSUPPORTED_VERSION"));
    }
}
