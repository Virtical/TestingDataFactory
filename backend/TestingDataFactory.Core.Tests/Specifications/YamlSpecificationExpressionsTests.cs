using System.Globalization;
using NUnit.Framework;
using TestingDataFactory.Core.Specifications.Model;
using TestingDataFactory.Core.Specifications.Parsing;

namespace TestingDataFactory.Core.Tests.Specifications;

public class YamlSpecificationExpressionsTests
{
    private readonly YamlSpecificationParser parser = new();

    [Test]
    public void ParsesDerivedRulesAndDictionaryFilter()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            types:
              money: { type: decimal, min: 0, scale: 2 }
              geo:
                enum:
                  dictionary: world_cities
                  where: country in ['RU', 'KZ', 'BY']
            tables:
              accounts:
                fields:
                  country: { type: geo, column: country }
                  amount: { type: money, derived: quantity * price }
                  quantity: { type: integer, min: 1 }
                  price: { type: money }
                  status: { type: string }
                  start_date: { type: date }
                  end_date: { type: date, null_rate: 0.4 }
                rules:
                  - expr: end_date == null || end_date >= start_date
                    message: Неверная дата
                  - when: status == 'cancelled'
                    expr: amount >= 0
                    severity: warning
            """);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var spec = result.Specification!;
        var table = spec.Tables["accounts"];
        Assert.That(table.Fields["amount"].Derived!.Source, Is.EqualTo("quantity * price"));
        Assert.That(table.Fields["amount"].Derived!.Dependencies, Is.EquivalentTo(new[] { "quantity", "price" }));
        Assert.That(table.Rules, Has.Count.EqualTo(2));
        Assert.That(table.Rules[0].Severity, Is.EqualTo(RuleSeverity.Error));
        Assert.That(table.Rules[0].Message, Is.EqualTo("Неверная дата"));
        Assert.That(table.Rules[1].Severity, Is.EqualTo(RuleSeverity.Warning));
        Assert.That(table.Rules[1].When!.Dependencies, Is.EquivalentTo(new[] { "status" }));
        Assert.That(((DictionaryEnumDefinition)spec.Types["geo"]).Where!.Dependencies, Is.EquivalentTo(new[] { "country" }));
    }

    [Test]
    public void SupportsDerivedWithoutExplicitType()
    {
        var result = parser.Parse(Document("{ derived: value * 2 }"));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var field = result.Specification!.Tables["items"].Fields["output"];
        Assert.That(field.Type, Is.EqualTo(FieldType.Dynamic));
        Assert.That(field.Derived!.Dependencies, Is.EquivalentTo(new[] { "value" }));
    }

    [TestCase("value + 1")]
    [TestCase("value == null ? 0 : value")]
    [TestCase("value in [1, 2, -3]")]
    [TestCase("size('abc') > 0")]
    [TestCase("'abc'.contains('b')")]
    [TestCase("'abc'.startsWith('a')")]
    [TestCase("'abc'.endsWith('c')")]
    [TestCase("'abc'.matches('a.*')")]
    [TestCase("value % 2 == 0 && !(value < 0)")]
    public void AcceptsAllowedCelOperations(string expr)
    {
        var result = parser.Parse(Document("{ derived: \"" + expr.Replace("\"", "\\\"") + "\" }"));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
    }

    [TestCase("unknown + 1")]
    [TestCase("other.value")]
    [TestCase("timestamp('2024-01-01T00:00:00Z')")]
    [TestCase("value[0]")]
    [TestCase("[1, 2].exists(x, x > 0)")]
    [TestCase("value in [value]")]
    [TestCase("value in value")]
    [TestCase("[1, 2]")]
    [TestCase("1u")]
    [TestCase("value +")]
    public void RejectsUnknownNamesAndUnsupportedCel(string expr)
    {
        var result = parser.Parse(Document("{ derived: \"" + expr + "\" }"));
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INVALID_CEL"));
    }

    [TestCase("{ expr: '1 + 2' }", "INVALID_CEL")]
    [TestCase("{ expr: 'value > 0', when: '2' }", "INVALID_CEL")]
    [TestCase("{ expr: 'value' }", "INVALID_CEL")]
    [TestCase("{ expr: 'value > 0', when: '(value)' }", "INVALID_CEL")]
    [TestCase("{ expr: 'value > 0', severity: fatal }", "INVALID_SEVERITY")]
    [TestCase("{ when: 'value > 0' }", "MISSING_RULE_EXPRESSION")]
    [TestCase("{ expr: 'missing > 0' }", "INVALID_CEL")]
    [TestCase("{ expr: 'value > 0', extra: 1 }", "UNSUPPORTED_PARAMETER")]
    public void RejectsInvalidRules(string rule, string code)
    {
        var result = parser.Parse("spec_version: '1.0'\ntables: { items: { fields: { value: { type: integer } }, rules: [" + rule + "] } }");
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(code));
    }

    [Test]
    public void RejectsIncompatibleExplicitDerivedType()
    {
        var result = parser.Parse(Document("{ type: boolean, derived: \"'text'\" }"));
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INVALID_CEL"));
    }

    [Test]
    public void ChecksRuleTypeAfterResolvingReferences()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              child:
                fields:
                  customer: { ref: parent.id }
                rules:
                  - expr: customer
              parent: { fields: { id: { type: integer } } }
            """);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INVALID_CEL"));
    }

    [Test]
    public void RejectsDictionaryColumnUsedAsBoolean()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            types: { geo: { enum: { dictionary: world_cities, where: country } } }
            tables: { t: { fields: { country: { type: geo, column: country } } } }
            """);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INVALID_CEL"));
    }

    [Test]
    public void RejectsDerivedCycles()
    {
        var yaml = """
            spec_version: '1.0'
            tables:
              t:
                fields:
                  a: { type: integer, derived: b + 1 }
                  b: { type: integer, derived: a + 1 }
            """;
        Assert.That(parser.Parse(yaml).Diagnostics.Select(d => d.Code), Does.Contain("DERIVED_CYCLE"));
    }

    [Test]
    public void RejectsUnknownDictionaryColumnAndNonBooleanFilter()
    {
        const string start = "spec_version: '1.0'\ntypes: { geo: { enum: { dictionary: world_cities, where: ";
        const string end = " } } }\ntables: { t: { fields: { country: { type: geo, column: country } } } }";
        Assert.That(parser.Parse(start + "'missing == 1'" + end).Diagnostics.Select(d => d.Code), Does.Contain("INVALID_CEL"));
        Assert.That(parser.Parse(start + "'1 + 2'" + end).Diagnostics.Select(d => d.Code), Does.Contain("INVALID_CEL"));
    }

    [Test]
    public void ParsesDecimalInRussianCultureAndRestoresIt()
    {
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            var result = parser.Parse(Document("{ type: decimal, derived: 'value / 100.0' }"));
            Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
            Assert.That(CultureInfo.CurrentCulture.Name, Is.EqualTo("ru-RU"));
        }
        finally { CultureInfo.CurrentCulture = before; }
    }

    private static string Document(string field) =>
        "spec_version: '1.0'\ntables: { items: { fields: { value: { type: integer }, output: " + field + " } } }";
}
