using NUnit.Framework;
using TestingDataFactory.Core.Specifications.Model;
using TestingDataFactory.Core.Specifications.Parsing;

namespace TestingDataFactory.Core.Tests.Specifications;

public class YamlSpecificationConstraintsTests
{
    private readonly YamlSpecificationParser parser = new();

    [Test]
    public void ParsesDateBoundsIncludingUnquotedYamlDates()
    {
        var result = parser.Parse(Document("{ type: date, min: 2024-02-29, max: '2025-03-01' }"));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var field = result.Specification!.Tables["items"].Fields["value"];
        Assert.That(field.TemporalMin, Is.EqualTo(new TemporalBound(new DateOnly(2024, 2, 29), null, null)));
        Assert.That(field.TemporalMax, Is.EqualTo(new TemporalBound(new DateOnly(2025, 3, 1), null, null)));
        Assert.That(field.Min, Is.Null);
    }

    [Test]
    public void ParsesDatetimeAndComparesOffsetsAsInstants()
    {
        var result = parser.Parse(Document("{ type: datetime, min: '2024-01-01T12:00:00+03:00', max: '2024-01-01T10:00:00Z' }"));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var bound = result.Specification!.Tables["items"].Fields["value"].TemporalMin!;
        Assert.That(bound.Date, Is.EqualTo(new DateOnly(2024, 1, 1)));
        Assert.That(bound.Time, Is.EqualTo(new TimeOnly(12, 0)));
        Assert.That(bound.UtcOffset, Is.EqualTo(TimeSpan.FromHours(3)));
    }

    [Test]
    public void ParsesDateOnlyDatetimeAndFractionalTime()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              events:
                fields:
                  created: { type: datetime, min: '2024-01-01', max: '2024-12-31' }
                  at: { type: time, min: '10:30:00.125', max: '12:00Z' }
            """);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INCOMPARABLE_TEMPORAL_BOUNDS"));
        var valid = parser.Parse(Document("{ type: datetime, min: '2024-01-01', max: '2024-12-31' }"));
        Assert.That(valid.IsSuccess, Is.True);
        Assert.That(valid.Specification!.Tables["items"].Fields["value"].TemporalMin!.Time, Is.EqualTo(TimeOnly.MinValue));
        var time = parser.Parse(Document("{ type: time, min: '10:30:00.125', max: '12:00:00.500' }"));
        Assert.That(time.IsSuccess, Is.True);
        Assert.That(time.Specification!.Tables["items"].Fields["value"].TemporalMin!.Time, Is.EqualTo(new TimeOnly(10, 30, 0, 125)));
    }

    [TestCase("date", "2023-02-29")]
    [TestCase("date", "2024-13-01")]
    [TestCase("date", "2024-01-01T10:00:00")]
    [TestCase("datetime", "2024-01-01 12:00:00")]
    [TestCase("datetime", "2024-01-01T25:00:00")]
    [TestCase("datetime", "2024-01-01T10:00:00+15:00")]
    [TestCase("time", "12:60:00")]
    [TestCase("time", "12:00:00.12345678")]
    [TestCase("time", "12:00:00+14:01")]
    [TestCase("time", "12:00:00junk")]
    public void RejectsInvalidTemporalBounds(string type, string value)
    {
        var result = parser.Parse(Document($"{{ type: {type}, min: '{value}' }}"));
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INVALID_TEMPORAL_BOUND"));
    }

    [TestCase("date", "2024-12-31", "2024-01-01")]
    [TestCase("datetime", "2024-01-01T10:00:00Z", "2024-01-01T12:00:00+03:00")]
    [TestCase("time", "23:59:59", "00:00:00")]
    public void RejectsReversedTemporalRanges(string type, string min, string max)
    {
        var result = parser.Parse(Document($"{{ type: {type}, min: '{min}', max: '{max}' }}"));
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INVALID_RANGE"));
    }

    [Test]
    public void RejectsComparingLocalAndOffsetDatetimes()
    {
        var result = parser.Parse(Document("{ type: datetime, min: '2024-01-01T10:00:00', max: '2024-01-01T12:00:00Z' }"));
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INCOMPARABLE_TEMPORAL_BOUNDS"));
    }

    [Test]
    public void InheritsAndOverridesDateBoundsInNamedTypes()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            types:
              period: { type: date, min: '2024-01-01', max: '2024-12-31' }
            tables:
              items:
                fields:
                  value: { type: period, min: '2024-06-01' }
            """);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var field = result.Specification!.Tables["items"].Fields["value"];
        Assert.That(field.TemporalMin!.Date, Is.EqualTo(new DateOnly(2024, 6, 1)));
        Assert.That(field.TemporalMax!.Date, Is.EqualTo(new DateOnly(2024, 12, 31)));
    }

    [TestCase("[", false)]
    [TestCase("^(abc)$", true)]
    [TestCase("(?<code>[0-9]+)", true)]
    public void ValidatesDotnetRegexSyntax(string pattern, bool valid)
    {
        var result = parser.Parse(Document("{ type: regex, pattern: '" + pattern + "' }"));
        Assert.That(result.IsSuccess, Is.EqualTo(valid));
        if (!valid) Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INVALID_REGEX"));
    }

    [Test]
    public void AppliesNumericAndStringConstraintsToReferences()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              child:
                fields:
                  amount: { ref: parent.amount, min: 2, max: 8, scale: 2 }
                  label: { ref: parent.label, min_length: 1, max_length: 10 }
              parent:
                fields:
                  amount: { type: decimal }
                  label: { type: string }
            """);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var fields = result.Specification!.Tables["child"].Fields;
        Assert.That(fields["amount"].Min, Is.EqualTo(2m));
        Assert.That(fields["amount"].Scale, Is.EqualTo(2));
        Assert.That(fields["label"].MaxLength, Is.EqualTo(10));
        Assert.That(fields["label"].Reference!.Table, Is.EqualTo("parent"));
    }

    [Test]
    public void ReportsInvalidConstraintsAcrossMultipleReferenceFields()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              child:
                fields:
                  a: { ref: parent.num, max_length: 10 }
                  b: { ref: parent.name, min_length: 8, max_length: 2 }
              parent:
                fields:
                  num: { type: integer }
                  name: { type: string }
            """);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Is.EquivalentTo(new[] { "INCOMPATIBLE_PARAMETER", "INVALID_RANGE" }));
        Assert.That(result.Diagnostics.Select(d => d.Path), Does.Contain("$.tables['child'].fields['a'].max_length"));
    }

    [Test]
    public void RejectsReferenceRangesDisjointFromTarget()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              child: { fields: { id: { ref: parent.id, min: 20 } } }
              parent: { fields: { id: { type: integer, min: 1, max: 10 } } }
            """);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INCOMPATIBLE_REFERENCE_RANGE"));
    }

    [Test]
    public void RejectsReferenceDateRangesDisjointFromTarget()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              child: { fields: { at: { ref: parent.at, min: '2025-01-01' } } }
              parent: { fields: { at: { type: date, max: '2024-12-31' } } }
            """);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INVALID_RANGE"));
    }

    [Test]
    public void ReportsInvalidInheritedRegexOverrideOnReference()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              child: { fields: { code: { ref: parent.code, pattern: '[' } } }
              parent: { fields: { code: { type: regex, pattern: '^x+$' } } }
            """);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INVALID_REGEX"));
    }

    [Test]
    public void ResolvesTemporalConstraintsForReferencesAndChains()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              c: { fields: { at: { ref: b.at, max: '2025-01-01' } } }
              b: { fields: { at: { ref: a.at, min: '2024-01-01' } } }
              a: { fields: { at: { type: date } } }
            """);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.That(result.Specification!.Tables["c"].Fields["at"].TemporalMax!.Date, Is.EqualTo(new DateOnly(2025, 1, 1)));
        Assert.That(result.Specification.Tables["b"].Fields["at"].TemporalMin!.Date, Is.EqualTo(new DateOnly(2024, 1, 1)));
    }

    private static string Document(string field) => "spec_version: '1.0'\ntables: { items: { fields: { value: " + field + " } } }";
}
