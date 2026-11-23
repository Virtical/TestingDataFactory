using NUnit.Framework;
using TestingDataFactory.Core.Specifications.Parsing;

namespace TestingDataFactory.Core.Tests.Specifications;

public class YamlSpecificationCelTypeTests
{
    private readonly YamlSpecificationParser parser = new();

    private const string Table = """
        spec_version: '1.0'
        tables:
          users:
            fields:
              age: { type: integer }
              price: { type: decimal }
              name: { type: string }
              active: { type: boolean }
              address:
                type: object
                fields:
                  city: { type: string }
                  location:
                    type: object
                    fields:
                      code: { type: integer }
              output: { type: TYPE, derived: EXPR }
        """;

    [TestCase("age + true")]
    [TestCase("name - 1")]
    [TestCase("active * age")]
    [TestCase("age && active")]
    [TestCase("age < name")]
    [TestCase("name.contains(age)")]
    [TestCase("age in ['one', 'two']")]
    [TestCase("address.missing")]
    [TestCase("address.city.missing")]
    [TestCase("name.city")]
    [TestCase("address.location.code == name")]
    [TestCase("address.location.code.contains('a')")]
    public void RejectsInvalidTypesAndSelections(string expression)
    {
        var result = parser.Parse(Table.Replace("TYPE", "integer").Replace("EXPR", "\"" + expression.Replace("\"", "\\\"") + "\""));
        Assert.That(result.IsSuccess, Is.False, expression);
        Assert.That(result.Diagnostics.Any(d => d.Code == "INVALID_CEL" && d.Path == "$.tables['users'].fields['output'].derived"), Is.True,
            string.Join("; ", result.Diagnostics));
    }

    [TestCase("age + price", "decimal")]
    [TestCase("address.city", "string")]
    [TestCase("address.location.code + age", "integer")]
    [TestCase("age == null ? 0 : age", "integer")]
    public void AcceptsCompatibleExpressions(string expression, string type)
    {
        var result = parser.Parse(Table.Replace("TYPE", type).Replace("EXPR", "\"" + expression.Replace("\"", "\\\"") + "\""));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
    }

    [Test]
    public void TracksRootDependencyForNestedSelectionsInDerivedAndRules()
    {
        var yaml = Table.Replace("TYPE", "integer").Replace("EXPR", "address.location.code + age") + "\n    rules:\n      - expr: address.city == 'Moscow'\n        when: address.location.code > 0\n";
        var result = parser.Parse(yaml);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        var table = result.Specification!.Tables["users"];
        Assert.That(table.Fields["output"].Derived!.Dependencies, Is.EquivalentTo(new[] { "address", "age" }));
        Assert.That(table.Rules[0].Expr.Dependencies, Is.EquivalentTo(new[] { "address" }));
        Assert.That(table.Rules[0].When!.Dependencies, Is.EquivalentTo(new[] { "address" }));
    }

    [TestCase("{ type: boolean, derived: 'age + 1' }")]
    [TestCase("{ type: string, derived: 'address.location.code' }")]
    public void RejectsIncompatibleDerivedResult(string output)
    {
        var yaml = Table.Replace("{ type: TYPE, derived: EXPR }", output);
        var result = parser.Parse(yaml);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INVALID_CEL"));
    }

    [Test]
    public void RejectsNonBooleanNestedRuleAndWhen()
    {
        var yaml = Table.Replace("TYPE", "integer").Replace("EXPR", "age") + "\n    rules:\n      - expr: address.city\n        when: age + 1\n";
        var result = parser.Parse(yaml);
        Assert.That(result.Diagnostics.Count(d => d.Code == "INVALID_CEL"), Is.EqualTo(2), string.Join("; ", result.Diagnostics));
    }

    [Test]
    public void ChecksKnownDictionaryColumnTypes()
    {
        var yaml = """
            spec_version: '1.0'
            types: { geo: { enum: { dictionary: world_cities, where: country + 1 } } }
            tables: { t: { fields: { x: { type: integer } } } }
            """;
        Assert.That(parser.Parse(yaml).Diagnostics.Select(d => d.Code), Does.Contain("INVALID_CEL"));
    }

    [Test]
    public void AcceptsSelectionsFromNamedObjectTypes()
    {
        var yaml = """
            spec_version: '1.0'
            types: { place: { type: object, fields: { city: { type: string } } } }
            tables:
              t:
                fields:
                  address: { type: place }
                  city: { type: string, derived: address.city }
                rules:
                  - expr: address.city.startsWith('M')
            """;
        var result = parser.Parse(yaml);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        Assert.That(result.Specification!.Tables["t"].Fields["city"].Derived!.Dependencies, Is.EqualTo(new[] { "address" }));
    }

    [Test]
    public void RejectsAccessThroughArray()
    {
        var yaml = """
            spec_version: '1.0'
            tables:
              t:
                fields:
                  addresses: { type: array, items: { type: object, fields: { city: { type: string } } } }
                  city: { type: string, derived: addresses.city }
            """;
        Assert.That(parser.Parse(yaml).Diagnostics.Select(d => d.Code), Does.Contain("INVALID_CEL"));
    }
}
