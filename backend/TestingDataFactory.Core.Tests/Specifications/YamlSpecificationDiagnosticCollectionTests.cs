using NUnit.Framework;
using TestingDataFactory.Core.Specifications.Parsing;

namespace TestingDataFactory.Core.Tests.Specifications;

public class YamlSpecificationDiagnosticCollectionTests
{
    private readonly YamlSpecificationParser parser = new();

    [Test]
    public void ReportsIndependentFieldAndExpressionErrorsInSameTable()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              t:
                fields:
                  broken: { type: integer, min: wrong }
                  age: { type: integer }
                  result: { type: integer, derived: age + true }
                rules:
                  - expr: missing > 0
            """);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => (d.Code, d.Path)), Does.Contain(("INVALID_PARAMETER", "$.tables['t'].fields['broken'].min")));
        Assert.That(result.Diagnostics.Select(d => (d.Code, d.Path)), Does.Contain(("INVALID_CEL", "$.tables['t'].fields['result'].derived")));
        Assert.That(result.Diagnostics.Select(d => (d.Code, d.Path)), Does.Contain(("INVALID_CEL", "$.tables['t'].rules[0].expr")));
    }

    [Test]
    public void SkipsExpressionsDependingOnInvalidFieldsAndChecksOtherExpressions()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              t:
                fields:
                  broken: { type: nonexistent }
                  name: { type: string }
                  deferred: { type: integer, derived: broken + 1 }
                  checked: { type: integer, derived: name - 1 }
                rules:
                  - expr: broken > 0
                    when: name - 1
            """);
        var celPaths = result.Diagnostics.Where(d => d.Code == "INVALID_CEL").Select(d => d.Path);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INVALID_FIELD_TYPE"));
        Assert.That(celPaths, Is.EquivalentTo(new[] { "$.tables['t'].fields['checked'].derived", "$.tables['t'].rules[0].when" }));
    }

    [Test]
    public void ChecksOtherTablesWhenReferenceCycleExists()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              a:
                fields:
                  link: { ref: b.link }
                  n: { type: integer }
                rules:
                  - expr: n + 1
                  - expr: link > 0
              b: { fields: { link: { ref: a.link } } }
              c:
                fields:
                  n: { type: integer }
                  bad: { type: integer, derived: n + true }
            """);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("REFERENCE_CYCLE"));
        Assert.That(result.Diagnostics.Where(d => d.Code == "INVALID_CEL").Select(d => d.Path),
            Is.EquivalentTo(new[] { "$.tables['a'].rules[0].expr", "$.tables['c'].fields['bad'].derived" }));
    }

    [Test]
    public void DoesNotInventTypeErrorsForReferencesToInvalidFields()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              parent: { fields: { id: { type: integer, min: wrong } } }
              child:
                fields:
                  ref_id: { ref: parent.id }
                  derived_id: { type: integer, derived: ref_id + 1 }
                rules:
                  - expr: ref_id > 0
            """);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INVALID_PARAMETER"));
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Not.Contain("INVALID_CEL"));
    }

    [Test]
    public void ReportsInvalidSyntaxEvenWhenExpressionMentionsUnavailableField()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              t:
                fields:
                  broken: { type: nonexistent }
                  result: { type: integer, derived: 'broken +' }
            """);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INVALID_FIELD_TYPE"));
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INVALID_CEL"));
    }

    [TestCase("{ type: integer, min: wrong, derived: 'age + true' }")]
    [TestCase("{ type: nonexistent, derived: 'age + true' }")]
    public void ChecksIndependentExpressionOnFieldWithInvalidDefinition(string definition)
    {
        var yaml = "spec_version: '1.0'\ntables: { t: { fields: { age: { type: integer }, value: " + definition + " } } }";
        var result = parser.Parse(yaml);
        Assert.That(result.Diagnostics.Any(d => d.Code == "INVALID_CEL" && d.Path == "$.tables['t'].fields['value'].derived"), Is.True,
            string.Join("; ", result.Diagnostics));
    }
}
