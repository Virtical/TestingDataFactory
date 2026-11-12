using NUnit.Framework;
using TestingDataFactory.Core.Specifications;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Tests.Specifications;

public class EnumGenerationTests
{
    private readonly SpecificationParser parser = new();

    [Test]
    public void GeneratesWeightedEnumWithoutZeroWeightAndReplaysSeed()
    {
        var parsed = Parse("status: { enum: { a: 0.2, b: 0.8, ignored: 0 } }", "{ type: status }");
        var first = parser.Generate(parsed, Options(10_000, 42));
        Assert.That(first.IsSuccess, Is.True, string.Join("; ", first.Diagnostics));
        var values = first.Rows!.Select(r => (string)r.Values["x"]!).ToArray();
        Assert.That(values.Count(v => v == "a"), Is.InRange(1800, 2200));
        Assert.That(values, Does.Not.Contain("ignored"));
        Assert.That(values, Is.EqualTo(parser.Generate(parsed, Options(10_000, 42)).Rows!.Select(r => r.Values["x"])));
    }

    [Test]
    public void SelectsUniqueValuesAndRejectsExhaustionBeforeGenerating()
    {
        var parsed = Parse("s: { enum: { a: 2, b: 5, c: 0 } }", "{ type: s, unique: true }");
        var success = parser.Generate(parsed, Options(2, 7));
        Assert.That(success.IsSuccess, Is.True, string.Join("; ", success.Diagnostics));
        Assert.That(success.Rows!.Select(row => row.Values["x"]), Is.EquivalentTo(new[] { "a", "b" }));
        var failure = parser.Generate(parsed, Options(3, 7));
        Assert.That(failure.Rows, Is.Null);
        Assert.That(failure.Diagnostics.Select(d => d.Code), Does.Contain("INSUFFICIENT_UNIQUE_VALUES"));
    }

    [Test]
    public void HandlesExtremeDecimalWeightsWithoutOverflow()
    {
        var parsed = Parse("s: { enum: { tiny: 0.0000000000000000000000000001, large: 79228162514264337593543950335 } }", "{ type: s, unique: true }");
        var result = parser.Generate(parsed, Options(2, 3));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        Assert.That(result.Rows!.Select(r => r.Values["x"]), Is.EquivalentTo(new[] { "tiny", "large" }));
    }

    [Test]
    public void GeneratesSimpleEnumsAndRespectsNullRate()
    {
        var parsed = Parse("s: { enum: [red, blue, green] }", "{ type: s, null_rate: 1 }");
        var result = parser.Generate(parsed, Options(20, 4));
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Rows!.All(row => row.Values["x"] is null), Is.True);
    }

    [Test]
    public void GeneratesAllUniqueSimpleEnumValues()
    {
        var parsed = Parse("s: { enum: [red, blue, green] }", "{ type: s, unique: true }");
        var result = parser.Generate(parsed, Options(3, 4));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        Assert.That(result.Rows!.Select(row => row.Values["x"]), Is.EquivalentTo(new[] { "red", "blue", "green" }));
    }

    [Test]
    public void RejectsDictionaryFilterBeforeStreaming()
    {
        var parsed = Parse("city: { enum: { dictionary: russian_cities, where: 'true' } }", "{ type: city }");
        var result = parser.Generate(parsed, Options(1, 1));
        Assert.That(result.Rows, Is.Null);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("UNSUPPORTED_GENERATION_FIELD"));
    }

    [Test]
    public void UsesBuiltInCitySetAndChecksCapacity()
    {
        var parsed = Parse("city: { enum: { dictionary: russian_cities } }", "{ type: city, unique: true }");
        var result = parser.Generate(parsed, Options(25, 3));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        var cities = result.Rows!.Select(r => (string)r.Values["x"]!).ToArray();
        Assert.That(cities.Distinct().Count(), Is.EqualTo(25));
        Assert.That(cities, Does.Contain("Екатеринбург"));
        Assert.That(parser.Generate(parsed, Options(26, 3)).Diagnostics.Select(d => d.Code), Does.Contain("INSUFFICIENT_UNIQUE_VALUES"));
    }

    [Test]
    public void RejectsUnsupportedMulticolumnDictionaryBeforeStreaming()
    {
        var parsed = Parse("geo: { enum: { dictionary: world_cities } }", "{ type: geo, column: city }");
        var result = parser.Generate(parsed, Options(1, 1));
        Assert.That(result.Rows, Is.Null);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("UNSUPPORTED_GENERATION_FIELD"));
    }

    private ParsedSpecification Parse(string types, string field)
    {
        var result = parser.Parse("spec_version: '1.0'\ntypes: { " + types + " }\ntables: { t: { fields: { x: " + field + " } } }");
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        return result.Specification!;
    }

    private static GenerationOverrides Options(int count, int seed) => new(Seed: seed, Rows: new Dictionary<string, int> { ["t"] = count });
}
