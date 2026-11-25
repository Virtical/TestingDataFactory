using NUnit.Framework;
using TestingDataFactory.Core.Generation;
using TestingDataFactory.Core.Specifications;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Tests.Specifications;

public class ExtendedGenerationTests
{
    private readonly SpecificationParser parser = new();

    [Test]
    public void GeneratesSemanticValuesWithLocaleAndReplaysSeed()
    {
        var spec = Parse("name: { generator: person.full_name }, email: { generator: internet.email, unique: true }, phone: { generator: phone, null_rate: 1 }");
        var first = Generate(spec, 30, 23);
        var second = Generate(spec, 30, 23);
        var rows = first.Rows!.ToArray();
        Assert.That(rows.Select(r => r.Values["name"]), Is.EqualTo(second.Rows!.Select(r => r.Values["name"])));
        Assert.That(rows.All(r => r.Values["name"] is string { Length: > 0 } && r.Values["phone"] is null), Is.True);
        Assert.That(rows.Select(r => (string)r.Values["email"]!).Distinct().Count(), Is.EqualTo(30));
    }

    [Test]
    public void ReportsFailureWhenSemanticConstraintsCannotBeSatisfied()
    {
        var result = Generate(Parse("name: { generator: person.full_name, max_length: 1 }"), 1, 7);
        var error = Assert.Throws<InvalidOperationException>(() => result.Rows!.ToArray());
        Assert.That(error!.Message, Does.Contain("name"));
    }

    [Test]
    public void GeneratesTemporalValuesWithinBoundsAndReplaysSeed()
    {
        const string fields = "day: { type: date, min: '2024-01-01', max: '2024-01-03', unique: true }, " +
                              "instant: { type: datetime, min: '2024-01-01T01:00:00+02:00', max: '2024-01-01T01:01:00+02:00' }, " +
                              "clock: { type: time, min: '10:00:00', max: '11:00:00' }";
        var spec = Parse(fields);
        var first = Generate(spec, 3, 100).Rows!.ToArray();
        var replay = Generate(spec, 3, 100).Rows!.ToArray();
        Assert.That(first.Select(r => r.Values["instant"]), Is.EqualTo(replay.Select(r => r.Values["instant"])));
        var days = first.Select(r => ((TemporalBound)r.Values["day"]!).Date!.Value).ToArray();
        Assert.That(days.Distinct().Count(), Is.EqualTo(3));
        Assert.That(days.All(d => d >= new DateOnly(2024, 1, 1) && d <= new DateOnly(2024, 1, 3)), Is.True);
        foreach (var row in first)
        {
            var value = (TemporalBound)row.Values["instant"]!;
            Assert.That(value.UtcOffset, Is.EqualTo(TimeSpan.FromHours(2)));
            Assert.That(value.Date, Is.EqualTo(new DateOnly(2024, 1, 1)));
            Assert.That(value.Time, Is.InRange(new TimeOnly(1, 0), new TimeOnly(1, 1)));
            var time = (TemporalBound)row.Values["clock"]!;
            Assert.That(time.UtcOffset, Is.Null);
            Assert.That(time.Time, Is.InRange(new TimeOnly(10, 0), new TimeOnly(11, 0)));
        }
    }

    [Test]
    public void RespectsDifferentTimeZoneBoundsByUtc()
    {
        var result = Generate(Parse("clock: { type: time, min: '23:00:00+02:00', max: '22:00:00Z' }"), 100, 3);
        foreach (var row in result.Rows!)
        {
            var value = (TemporalBound)row.Values["clock"]!;
            var utc = value.Time!.Value.Ticks - value.UtcOffset!.Value.Ticks;
            Assert.That(utc, Is.InRange(TimeSpan.FromHours(21).Ticks, TimeSpan.FromHours(22).Ticks));
        }
    }

    [Test]
    public void HandlesTimeZonesSpanningBothSidesOfTheUtcDay()
    {
        var result = Generate(Parse("clock: { type: time, min: '00:00:00+14:00', max: '23:00:00-14:00' }"), 500, 13);
        foreach (var row in result.Rows!)
        {
            var value = (TemporalBound)row.Values["clock"]!;
            var utc = value.Time!.Value.Ticks - value.UtcOffset!.Value.Ticks;
            Assert.That(utc, Is.InRange(TimeSpan.FromHours(-14).Ticks, TimeSpan.FromHours(37).Ticks));
            Assert.That(value.UtcOffset.Value.Duration(), Is.LessThanOrEqualTo(TimeSpan.FromHours(14)));
        }
    }

    [Test]
    public void UsesDocumentedTemporalDefaultsAndHandlesSingleBounds()
    {
        var result = Generate(Parse("day: { type: date }, early: { type: date, max: '1900-01-01' }, late: { type: datetime, min: '2040-01-01' }, clock: { type: time }"), 20, 5);
        foreach (var row in result.Rows!)
        {
            var day = ((TemporalBound)row.Values["day"]!).Date!.Value;
            Assert.That(day, Is.InRange(new DateOnly(2000, 1, 1), new DateOnly(2030, 12, 31)));
            Assert.That(((TemporalBound)row.Values["early"]!).Date, Is.EqualTo(new DateOnly(1900, 1, 1)));
            Assert.That(((TemporalBound)row.Values["late"]!).Date, Is.EqualTo(new DateOnly(2040, 1, 1)));
            Assert.That(((TemporalBound)row.Values["clock"]!).Time, Is.Not.Null);
        }
    }

    [Test]
    public void RejectsTooManyUniqueDatesBeforeGeneration()
    {
        var parsed = Parse("day: { type: date, min: '2024-01-01', max: '2024-01-02', unique: true }");
        var result = parser.Generate(parsed, Options(3, 1));
        Assert.That(result.Rows, Is.Null);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INSUFFICIENT_UNIQUE_VALUES"));
    }

    [Test]
    public void GeneratesIntegerAndDecimalNormalDistributions()
    {
        var spec = Parse("age: { type: integer, min: 0, max: 100, distribution: { normal: { mean: 35, std: 10 } } }, " +
                         "price: { type: decimal, min: -50, max: 50, scale: 2, distribution: { normal: { mean: 10, std: 4 } } }");
        var rows = Generate(spec, 10_000, 9).Rows!.ToArray();
        var ages = rows.Select(r => (decimal)r.Values["age"]!).ToArray();
        var prices = rows.Select(r => (decimal)r.Values["price"]!).ToArray();
        Assert.That(ages.Average(), Is.InRange(34m, 36m));
        Assert.That(prices.Average(), Is.InRange(9.8m, 10.2m));
        var mean = ages.Average();
        var standardDeviation = Math.Sqrt(ages.Select(v => Math.Pow((double)(v - mean), 2)).Average());
        Assert.That(standardDeviation, Is.InRange(9.3, 10.5));
        Assert.That(ages.All(v => v is >= 0 and <= 100 && decimal.Truncate(v) == v), Is.True);
        Assert.That(prices.All(v => v is >= -50 and <= 50 && decimal.Round(v, 2) == v), Is.True);
        var replay = Generate(spec, 10_000, 9).Rows!.Take(5).Select(r => r.Values["price"]);
        Assert.That(replay, Is.EqualTo(rows.Take(5).Select(r => r.Values["price"])));
    }

    [Test]
    public void NormalUniqueValuesAreUniqueWithinBounds()
    {
        var result = Generate(Parse("x: { type: integer, min: 0, max: 20, unique: true, distribution: { normal: { mean: 10, std: 5 } } }"), 10, 8);
        var values = result.Rows!.Select(r => (decimal)r.Values["x"]!).ToArray();
        Assert.That(values.Distinct().Count(), Is.EqualTo(10));
        Assert.That(values.All(v => v is >= 0 and <= 20), Is.True);
    }

    [Test]
    public void NormalSamplingFailureIsBoundedAndNamesField()
    {
        var result = Generate(Parse("x: { type: integer, min: 0, max: 1, distribution: { normal: { mean: 100000, std: 1 } } }"), 1, 3);
        var error = Assert.Throws<InvalidOperationException>(() => result.Rows!.ToArray());
        Assert.That(error!.Message, Does.Contain("x").And.Contain("10000"));
    }

    private ParsedSpecification Parse(string fields)
    {
        var result = parser.Parse("spec_version: '1.0'\ntables: { t: { fields: { " + fields + " } } }");
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        return result.Specification!;
    }

    private GenerationResult Generate(ParsedSpecification spec, int count, int seed)
    {
        var result = parser.Generate(spec, Options(count, seed));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        return result;
    }

    private static GenerationOverrides Options(int count, int seed) => new(Seed: seed, Rows: new Dictionary<string, int> { ["t"] = count });
}
