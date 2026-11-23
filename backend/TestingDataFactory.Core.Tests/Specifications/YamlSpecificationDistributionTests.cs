using NUnit.Framework;
using TestingDataFactory.Core.Specifications.Model;
using TestingDataFactory.Core.Specifications.Parsing;

namespace TestingDataFactory.Core.Tests.Specifications;

public class YamlSpecificationDistributionTests
{
    private readonly YamlSpecificationParser parser = new();

    [TestCase("integer")]
    [TestCase("decimal")]
    public void UsesUniformByDefaultForNumericFields(string type)
    {
        var field = ParseField("{ type: " + type + " }");
        Assert.That(field.Distribution, Is.TypeOf<UniformDistribution>());
    }

    [TestCase("integer", "uniform", typeof(UniformDistribution))]
    [TestCase("decimal", "lognormal", typeof(LogNormalDistribution))]
    public void ParsesSimpleDistributions(string type, string distribution, Type expected)
    {
        var field = ParseField($"{{ type: {type}, distribution: {distribution} }}");
        Assert.That(field.Distribution, Is.TypeOf(expected));
    }

    [Test]
    public void ParsesNormalDistributionWithInvariantDecimals()
    {
        var field = ParseField("{ type: integer, distribution: { normal: { mean: -3.5, std: 1.25 } } }");
        Assert.That(field.Distribution, Is.EqualTo(new NormalDistribution(-3.5m, 1.25m)));
    }

    [Test]
    public void SupportsNamedNumericTypesAndReferences()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            types:
              money: { type: decimal, min: 0, scale: 2 }
            tables:
              child: { fields: { price: { ref: parent.price, distribution: { normal: { mean: 35, std: 12 } } } } }
              parent: { fields: { price: { type: money, distribution: lognormal } } }
            """);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.That(result.Specification!.Tables["parent"].Fields["price"].Distribution, Is.TypeOf<LogNormalDistribution>());
        Assert.That(result.Specification.Tables["child"].Fields["price"].Distribution, Is.EqualTo(new NormalDistribution(35, 12)));
    }

    [Test]
    public void DefaultOnReferenceDoesNotInheritTargetDistribution()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              child: { fields: { n: { ref: parent.n } } }
              parent: { fields: { n: { type: decimal, distribution: lognormal } } }
            """);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Specification!.Tables["child"].Fields["n"].Distribution, Is.TypeOf<UniformDistribution>());
    }

    [Test]
    public void RejectsDistributionOnNonNumericReference()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              child: { fields: { label: { ref: parent.label, distribution: uniform } } }
              parent: { fields: { label: { type: string } } }
            """);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INCOMPATIBLE_DISTRIBUTION"));
    }

    [Test]
    public void RejectsDistributionInsideTypeDeclaration()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            types: { custom: { type: integer, distribution: uniform } }
            tables: { items: { fields: { value: { type: custom } } } }
            """);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("UNSUPPORTED_PARAMETER"));
    }

    [TestCase("{ type: integer, distribution: normal }", "INVALID_DISTRIBUTION")]
    [TestCase("{ type: integer, distribution: exponential }", "INVALID_DISTRIBUTION")]
    [TestCase("{ type: integer, distribution: [] }", "INVALID_DISTRIBUTION")]
    [TestCase("{ type: integer, distribution: null }", "INVALID_DISTRIBUTION")]
    [TestCase("{ type: integer, distribution: { uniform: {} } }", "INVALID_DISTRIBUTION")]
    [TestCase("{ type: integer, distribution: { normal: [] } }", "INVALID_DISTRIBUTION")]
    [TestCase("{ type: integer, distribution: { normal: { mean: 0 } } }", "MISSING_DISTRIBUTION_PARAMETER")]
    [TestCase("{ type: integer, distribution: { normal: { std: 1 } } }", "MISSING_DISTRIBUTION_PARAMETER")]
    [TestCase("{ type: integer, distribution: { normal: { mean: '0', std: 1 } } }", "INVALID_PARAMETER")]
    [TestCase("{ type: integer, distribution: { normal: { mean: 0, std: 0 } } }", "INVALID_STANDARD_DEVIATION")]
    [TestCase("{ type: integer, distribution: { normal: { mean: 0, std: -1 } } }", "INVALID_STANDARD_DEVIATION")]
    [TestCase("{ type: integer, distribution: { normal: { mean: 0, std: 1, extra: 2 } } }", "UNSUPPORTED_PARAMETER")]
    [TestCase("{ type: string, distribution: uniform }", "INCOMPATIBLE_DISTRIBUTION")]
    [TestCase("{ type: boolean, distribution: lognormal }", "INCOMPATIBLE_DISTRIBUTION")]
    [TestCase("{ generator: person.full_name, distribution: uniform }", "INCOMPATIBLE_DISTRIBUTION")]
    [TestCase("{ type: integer, derived: '1', distribution: uniform }", "INCOMPATIBLE_DISTRIBUTION")]
    public void RejectsInvalidDistributions(string declaration, string code)
    {
        var result = parser.Parse(Document(declaration));
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Specification, Is.Null);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(code));
    }

    [Test]
    public void NonNumericAndDerivedFieldsHaveNoDistribution()
    {
        Assert.That(ParseField("{ type: string }").Distribution, Is.Null);
        Assert.That(ParseField("{ type: integer, derived: '42' }").Distribution, Is.Null);
    }

    [Test]
    public void ReportsNormalParameterPathAndPosition()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              items:
                fields:
                  value: { type: decimal, distribution: { normal: { mean: 0, std: 0 } } }
            """);
        var error = result.Diagnostics.Single(d => d.Code == "INVALID_STANDARD_DEVIATION");
        Assert.That(error.Path, Is.EqualTo("$.tables['items'].fields['value'].distribution.normal.std"));
        Assert.That(error.Line, Is.EqualTo(5));
        Assert.That(error.Column, Is.GreaterThan(1));
    }

    private FieldDefinition ParseField(string field)
    {
        var result = parser.Parse(Document(field));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        return result.Specification!.Tables["items"].Fields["value"];
    }

    private static string Document(string field) => "spec_version: '1.0'\ntables: { items: { fields: { value: " + field + " } } }";
}
