using NUnit.Framework;
using TestingDataFactory.Core.Specifications;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Tests.Specifications;

public class SingleTableGenerationTests
{
    private readonly SpecificationParser parser = new();

    [Test]
    public void ReplaysGeneratedSeedAndReturnsSuppliedSeed()
    {
        var parsed = Parse("id: { type: uuid, primary: true }, n: { type: integer, min: -5, max: 8 }, flag: { type: boolean }");
        var randomSeed = parser.Generate(parsed, Options(20));
        Assert.That(randomSeed.IsSuccess, Is.True, string.Join("; ", randomSeed.Diagnostics));
        Assert.That(randomSeed.Seed, Is.Not.Null);
        var replay = parser.Generate(parsed, Options(20, randomSeed.Seed));
        Assert.That(replay.Seed, Is.EqualTo(randomSeed.Seed));
        Assert.That(Values(replay), Is.EqualTo(Values(randomSeed)));
        Assert.That(Values(randomSeed), Is.EqualTo(Values(randomSeed)), "Повторное перечисление использует тот же seed.");
    }

    [TestCase(0)]
    [TestCase(-2147483648)]
    [TestCase(42)]
    public void ReturnsExplicitSeedAndGeneratesValidValues(int seed)
    {
        var parsed = Parse("id: { type: uuid, primary: true }, n: { type: integer, min: 1, max: 100, unique: true }, flag: { type: boolean, true_rate: 0 }");
        var result = parser.Generate(parsed, Options(100, seed));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        Assert.That(result.Seed, Is.EqualTo(seed));
        var rows = result.Rows!.ToArray();
        Assert.That(rows, Has.Length.EqualTo(100));
        Assert.That(rows.All(r => r.Table == "t" && r.Values["flag"] is false), Is.True);
        Assert.That(rows.Select(r => (decimal)r.Values["n"]!).Distinct().Count(), Is.EqualTo(100));
        Assert.That(rows.All(r => (decimal)r.Values["n"]! is >= 1 and <= 100), Is.True);
        var ids = rows.Select(r => (Guid)r.Values["id"]!).ToArray();
        Assert.That(ids.Distinct().Count(), Is.EqualTo(100));
        Assert.That(ids.All(id => id.ToString()[14] == '4' && "89ab".Contains(id.ToString()[19])), Is.True);
    }

    [TestCase("{ type: integer }", 0, 100)]
    [TestCase("{ type: integer, min: 200 }", 200, 200)]
    [TestCase("{ type: integer, max: -10 }", -10, -10)]
    [TestCase("{ type: integer, min: -5 }", -5, 100)]
    [TestCase("{ type: integer, max: 5 }", 0, 5)]
    public void AppliesDefaultIntegerBounds(string field, int min, int max)
    {
        var result = parser.Generate(Parse("x: " + field), Options(50, 5));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        Assert.That(result.Rows!.All(r => (decimal)r.Values["x"]! >= min && (decimal)r.Values["x"]! <= max), Is.True);
    }

    [Test]
    public void UsesYamlSeedAndHonoursOverride()
    {
        var parsed = parser.Parse("spec_version: '1.0'\ntables: { t: { fields: { x: { type: integer } } } }\ngeneration: { seed: 123, rows: { t: 3 } }");
        Assert.That(parsed.IsSuccess, Is.True);
        Assert.That(parser.Generate(parsed.Specification!).Seed, Is.EqualTo(123));
        Assert.That(parser.Generate(parsed.Specification!, new GenerationOverrides(Seed: 999)).Seed, Is.EqualTo(999));
    }

    [Test]
    public void SupportsNullRateAndUniqueBoolean()
    {
        var parsed = Parse("empty: { type: uuid, null_rate: 1 }, flag: { type: boolean, unique: true }");
        var result = parser.Generate(parsed, Options(2, 7));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        var rows = result.Rows!.ToArray();
        Assert.That(rows.All(row => row.Values["empty"] is null), Is.True);
        Assert.That(rows.Select(row => (bool)row.Values["flag"]!), Is.EquivalentTo(new[] { true, false }));
    }

    [TestCase("x: { type: boolean, unique: true }", 3, "INSUFFICIENT_UNIQUE_VALUES")]
    [TestCase("x: { type: boolean, unique: true, true_rate: 1 }", 2, "INSUFFICIENT_UNIQUE_VALUES")]
    [TestCase("x: { type: integer, min: 4, max: 5, unique: true, null_rate: 0.5 }", 3, "UNSUPPORTED_NULLABLE_UNIQUE_CAPACITY")]
    [TestCase("x: { type: integer, unique: true }", 102, "INSUFFICIENT_UNIQUE_VALUES")]
    [TestCase("x: { type: string }", 1, "UNSUPPORTED_GENERATION_FIELD")]
    public void RejectsUnsupportedOrUnsafeJobsBeforeReturningRows(string fields, int count, string code)
    {
        var result = parser.Generate(Parse(fields), Options(count, 1));
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Rows, Is.Null);
        Assert.That(result.Seed, Is.Null);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(code));
    }

    [Test]
    public void RejectsLogicalErrorsBeforeGeneration()
    {
        var parsed = parser.Parse("spec_version: '1.0'\ntables: { t: { fields: { x: { ref: absent.id } } } }");
        Assert.That(parsed.IsSuccess, Is.True);
        var result = parser.Generate(parsed.Specification!, Options(1));
        Assert.That(result.Rows, Is.Null);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("UNKNOWN_REFERENCE_TABLE"));
    }

    [Test]
    public void GeneratesLargeUniqueSequenceLazily()
    {
        var result = parser.Generate(Parse("x: { type: integer, min: 0, max: 99999999, unique: true }"), Options(100_000_000, 10));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        var first = result.Rows!.Take(100).Select(row => row.Values["x"]).ToArray();
        Assert.That(first.Distinct().Count(), Is.EqualTo(100));
    }

    [Test]
    public void ValidatesMissingCountUnsupportedModeAndOutput()
    {
        var parsed = Parse("x: { type: integer }");
        Assert.That(parser.Generate(parsed).Diagnostics.Select(d => d.Code), Does.Contain("MISSING_ROW_COUNT"));
        var result = parser.Generate(parsed, new GenerationOverrides(Rows: new Dictionary<string, int> { ["t"] = 1 }, Mode: GenerationMode.EdgeCases, Output: new JsonOutput()));
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("UNSUPPORTED_GENERATION_MODE"));
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("UNSUPPORTED_OUTPUT"));
        Assert.That(result.Rows, Is.Null);
    }

    [Test]
    public void RejectsRulesAndMissingRootCountsBeforeReturningRows()
    {
        var rules = parser.Parse("spec_version: '1.0'\ntables: { t: { fields: { x: { type: integer } }, rules: [{ expr: 'x >= 0' }] } }");
        var rulesResult = parser.Generate(rules.Specification!, Options(3));
        Assert.That(rulesResult.Rows, Is.Null);
        Assert.That(rulesResult.Diagnostics.Select(d => d.Code), Does.Contain("UNSUPPORTED_GENERATION_RULES"));

        var tables = parser.Parse("spec_version: '1.0'\ntables: { t: { fields: { x: { type: integer } } }, u: { fields: { y: { type: integer } } } }");
        var tableResult = parser.Generate(tables.Specification!, Options(3));
        Assert.That(tableResult.Rows, Is.Null);
        Assert.That(tableResult.Diagnostics.Select(d => d.Code), Does.Contain("MISSING_ROW_COUNT"));
    }

    [Test]
    public void UniqueNullableFieldCanProduceOnlyNulls()
    {
        var result = parser.Generate(Parse("x: { type: integer, min: 1, max: 1, unique: true, null_rate: 1 }"), Options(5, 1));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        Assert.That(result.Rows!.All(row => row.Values["x"] is null), Is.True);
    }

    [Test]
    public void GeneratesAcrossEntireDecimalIntegerRange()
    {
        var parsed = Parse("x: { type: integer, min: -79228162514264337593543950335, max: 79228162514264337593543950335, unique: true }");
        var result = parser.Generate(parsed, Options(20, 5));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        Assert.That(result.Rows!.Select(row => (decimal)row.Values["x"]!).Distinct().Count(), Is.EqualTo(20));
    }

    private ParsedSpecification Parse(string fields)
    {
        var result = parser.Parse("spec_version: '1.0'\ntables: { t: { fields: { " + fields + " } } }");
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        return result.Specification!;
    }

    private static GenerationOverrides Options(int count, int? seed = null) =>
        new(Seed: seed, Rows: new Dictionary<string, int> { ["t"] = count });

    private static object?[][] Values(TestingDataFactory.Core.Generation.GenerationResult result) =>
        result.Rows!.Select(row => row.Values.Values.ToArray()).ToArray();
}
