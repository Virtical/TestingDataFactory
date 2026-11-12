using NUnit.Framework;
using TestingDataFactory.Core.Specifications;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Tests.Specifications;

public class SpecificationValidationApiTests
{
    private readonly SpecificationParser parser = new();

    [TestCase("tables: { t: { fields: { id: { ref: absent.id } } } }", "UNKNOWN_REFERENCE_TABLE")]
    [TestCase("tables: { a: { fields: { id: { ref: b.id } } }, b: { fields: { id: { ref: a.id } } } }", "REFERENCE_CYCLE")]
    [TestCase("tables: { t: { fields: { a: { type: integer, derived: b + 1 }, b: { type: integer, derived: a + 1 } } } }", "DERIVED_CYCLE")]
    [TestCase("tables: { t: { fields: { id: { type: integer } }, rules: [{ expr: 'unknown > 0' }] } }", "INVALID_CEL")]
    public void ParseKeepsLogicalErrorsForValidation(string body, string code)
    {
        var parsed = parser.Parse("spec_version: '1.0'\n" + body);
        Assert.That(parsed.IsSuccess, Is.True, string.Join("; ", parsed.Diagnostics));
        var validation = parser.Validate(parsed.Specification!);
        Assert.That(validation.IsSuccess, Is.False);
        Assert.That(validation.Diagnostics.Select(d => d.Code), Does.Contain(code));
    }

    [Test]
    public void ParseStillRejectsMalformedExpression()
    {
        var result = parser.Parse("spec_version: '1.0'\ntables: { t: { fields: { x: { type: integer, derived: '1 +' } } } }");
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INVALID_CEL"));
    }

    [Test]
    public void ConstructorAndGenerationValidationUseDifferentParameters()
    {
        const string yaml = """
            spec_version: '1.0'
            tables: { t: { fields: { id: { type: integer, min: 1, max: 2, unique: true } } } }
            generation: { seed: 42, rows: { t: 2 }, output: { json: {} } }
            """;
        var parsed = parser.Parse(yaml);
        Assert.That(parsed.IsSuccess, Is.True, string.Join("; ", parsed.Diagnostics));
        Assert.That(parser.Validate(parsed.Specification!).IsSuccess, Is.True);
        var valid = parser.Validate(parsed.Specification!, new GenerationOverrides());
        Assert.That(valid.IsSuccess, Is.True, string.Join("; ", valid.Diagnostics));
        Assert.That(valid.Options!.Seed, Is.EqualTo(42));
        Assert.That(valid.Options.Rows["t"], Is.EqualTo(2));
        var invalid = parser.Validate(parsed.Specification!, new GenerationOverrides(Rows: new Dictionary<string, int> { ["t"] = 3 }));
        Assert.That(invalid.IsSuccess, Is.False);
        Assert.That(invalid.Diagnostics.Any(d => d.Code == "INSUFFICIENT_UNIQUE_VALUES" && d.Path == "$.tables['t'].fields['id']"), Is.True);
    }

    [Test]
    public void ValidationResolvesForwardReferencesAndReturnsOrderedModel()
    {
        var parsed = parser.Parse("spec_version: '1.0'\ntables: { child: { fields: { id: { ref: parent.id } } }, parent: { fields: { id: { type: integer } } } }");
        Assert.That(parsed.IsSuccess, Is.True);
        var validated = parser.Validate(parsed.Specification!, new GenerationOverrides(Rows: new Dictionary<string, int> { ["parent"] = 1 }));
        Assert.That(validated.IsSuccess, Is.True, string.Join("; ", validated.Diagnostics));
        Assert.That(validated.Specification!.TableOrder, Is.EqualTo(new[] { "parent", "child" }));
        Assert.That(validated.Specification.Tables["child"].Fields["id"].Type, Is.EqualTo(FieldType.Integer));
    }

    [Test]
    public void GenerationOptionsRejectRowsForChildAndUnknownTable()
    {
        var parsed = parser.Parse("spec_version: '1.0'\ntables: { parent: { fields: { id: { type: integer } } }, child: { fields: { id: { ref: parent.id } } } }");
        var result = parser.Validate(parsed.Specification!, new GenerationOverrides(Rows: new Dictionary<string, int> { ["child"] = 1, ["missing"] = 1 }));
        Assert.That(result.Diagnostics.Select(d => d.Code), Is.EquivalentTo(new[] { "ROWS_FOR_CHILD_TABLE", "UNKNOWN_ROWS_TABLE" }));
    }

    [Test]
    public void DoesNotClaimNullableUniqueRangeIsImpossible()
    {
        var parsed = parser.Parse("spec_version: '1.0'\ntables: { t: { fields: { id: { type: integer, min: 1, max: 2, unique: true, null_rate: 0.5 } } } }");
        var result = parser.Validate(parsed.Specification!, new GenerationOverrides(Rows: new Dictionary<string, int> { ["t"] = 3 }));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
    }

    [TestCase(3, true)]
    [TestCase(4, false)]
    [TestCase(0, true)]
    public void ChecksInclusiveIntegerCapacityAcrossZero(int count, bool expected)
    {
        var parsed = parser.Parse("spec_version: '1.0'\ntables: { t: { fields: { id: { type: integer, min: -2, max: 0, primary: true } } } }");
        var result = parser.Validate(parsed.Specification!, new GenerationOverrides(Rows: new Dictionary<string, int> { ["t"] = count }));
        Assert.That(result.IsSuccess, Is.EqualTo(expected), string.Join("; ", result.Diagnostics));
    }

    [Test]
    public void RejectsInvalidReferenceParametersAtParseTime()
    {
        var result = parser.Parse("spec_version: '1.0'\ntables: { t: { fields: { id: { ref: other.id, type: integer } } } }");
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("UNSUPPORTED_PARAMETER"));
    }

    [Test]
    public void CapacityCheckHandlesMaximumDecimalWithoutOverflow()
    {
        const string yaml = """
            spec_version: '1.0'
            tables: { t: { fields: { id: { type: integer, min: 79228162514264337593543950335, max: 79228162514264337593543950335, unique: true } } } }
            """;
        var parsed = parser.Parse(yaml);
        Assert.That(parsed.IsSuccess, Is.True, string.Join("; ", parsed.Diagnostics));
        var result = parser.Validate(parsed.Specification!, new GenerationOverrides(Rows: new Dictionary<string, int> { ["t"] = 2 }));
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INSUFFICIENT_UNIQUE_VALUES"));
    }
}
