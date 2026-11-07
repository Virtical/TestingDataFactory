using NUnit.Framework;
using TestingDataFactory.Core.Specifications.Model;
using TestingDataFactory.Core.Specifications.Parsing;

namespace TestingDataFactory.Core.Tests.Specifications;

public class YamlSpecificationParserTests
{
    private readonly YamlSpecificationParser parser = new();

    [Test]
    public void ParsesSeveralIndependentTables()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              customers:
                fields:
                  id: { type: integer, primary: true }
                  name: { type: string, max_length: 100 }
              orders:
                fields:
                  id: { type: uuid, primary: true }
                  total: { type: decimal, min: 0, scale: 2 }
            """);

        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var tables = result.Specification!.Tables;
        Assert.That(tables.Keys, Is.EquivalentTo(new[] { "customers", "orders" }));
        Assert.That(tables["customers"].Fields["id"].Type, Is.EqualTo(FieldType.Integer));
        Assert.That(tables["orders"].Fields["id"].Type, Is.EqualTo(FieldType.Uuid));
        Assert.That(tables["orders"].Fields["total"].Scale, Is.EqualTo(2));
    }

    [Test]
    public void CollectsErrorsFromDifferentTables()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              first:
                fields:
                  bad: { type: integer, min: wrong }
              second:
                fields:
                  bad: { type: string, max_length: -1 }
              third: []
            """);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Specification, Is.Null);
        Assert.That(result.Diagnostics.Select(d => d.Path), Is.EquivalentTo(new[]
        {
            "$.tables['first'].fields['bad'].min",
            "$.tables['second'].fields['bad'].max_length",
            "$.tables['third']"
        }));
        Assert.That(result.Diagnostics.Select(d => d.Line), Is.EquivalentTo(new int?[] { 5, 8, 9 }));
    }

    [Test]
    public void RejectsDuplicateTableNames()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              users: { fields: { id: { type: integer } } }
              users: { fields: { id: { type: uuid } } }
            """);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics, Is.Not.Empty);
    }

    [Test]
    public void RejectsDuplicateNamesRegardlessOfQuoting()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              users: { fields: { id: { type: integer } } }
              'users': { fields: { id: { type: uuid } } }
            """);

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics, Is.Not.Empty);
    }

    [Test]
    public void ParsesSingleTableAndSupportedParameters()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            locale: en_US
            tables:
              users:
                fields:
                  id: { type: integer, primary: true, min: 1, max: 500 }
                  name: { type: string, min_length: 1, max_length: 100, pii: true }
                  amount: { type: decimal, min: 0, scale: 2 }
                  code: { type: regex, pattern: '^\d{10}$' }
                  active: { type: boolean, true_rate: 0.85 }
                  created: { type: datetime, null_rate: 0.1 }
            """);

        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var specification = result.Specification!;
        Assert.That(specification.Version, Is.EqualTo("1.0"));
        Assert.That(specification.Locale, Is.EqualTo("en_US"));
        var table = specification.Tables["users"];
        Assert.That(table.Name, Is.EqualTo("users"));
        Assert.That(table.Fields["id"].Unique, Is.True);
        Assert.That(table.Fields["id"].Min, Is.EqualTo(1m));
        Assert.That(table.Fields["name"].MaxLength, Is.EqualTo(100));
        Assert.That(table.Fields["name"].Pii, Is.True);
        Assert.That(table.Fields["amount"].Scale, Is.EqualTo(2));
        Assert.That(table.Fields["code"].Pattern, Is.EqualTo("^\\d{10}$"));
        Assert.That(table.Fields["active"].TrueRate, Is.EqualTo(0.85m));
        Assert.That(table.Fields["created"].Type, Is.EqualTo(FieldType.DateTime));
    }

    [TestCase("integer", FieldType.Integer)]
    [TestCase("decimal", FieldType.Decimal)]
    [TestCase("string", FieldType.String)]
    [TestCase("boolean", FieldType.Boolean)]
    [TestCase("date", FieldType.Date)]
    [TestCase("datetime", FieldType.DateTime)]
    [TestCase("time", FieldType.Time)]
    [TestCase("uuid", FieldType.Uuid)]
    public void ParsesBasicTypes(string name, FieldType expected)
    {
        var result = parser.Parse(Document("{ type: " + name + " }"));
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Specification!.Tables["items"].Fields["value"].Type, Is.EqualTo(expected));
    }

    [TestCase("{ type: missing }", "INVALID_FIELD_TYPE")]
    [TestCase("{}", "INVALID_FIELD_TYPE")]
    [TestCase("[]", "FIELD_NOT_MAPPING")]
    [TestCase("{ type: integer, min: 5, max: 2 }", "INVALID_RANGE")]
    [TestCase("{ type: integer, min: 1.5 }", "INVALID_INTEGER_RANGE")]
    [TestCase("{ type: integer, min: '3' }", "INVALID_PARAMETER")]
    [TestCase("{ type: string, min_length: 9, max_length: 2 }", "INVALID_RANGE")]
    [TestCase("{ type: string, max_length: -1 }", "INVALID_PARAMETER")]
    [TestCase("{ type: decimal, scale: 1.5 }", "INVALID_PARAMETER")]
    [TestCase("{ type: integer, scale: 2 }", "INCOMPATIBLE_PARAMETER")]
    [TestCase("{ type: boolean, true_rate: 1.1 }", "INVALID_RATE")]
    [TestCase("{ type: integer, null_rate: -0.1 }", "INVALID_RATE")]
    [TestCase("{ type: integer, primary: true, null_rate: 0.1 }", "PRIMARY_NULLABLE")]
    [TestCase("{ type: integer, primary: 'true' }", "INVALID_PARAMETER")]
    [TestCase("{ type: regex }", "MISSING_PATTERN")]
    [TestCase("{ type: regex, pattern: 123 }", "INVALID_PARAMETER")]
    [TestCase("{ type: string, pattern: x }", "INCOMPATIBLE_PARAMETER")]
    [TestCase("{ type: string, typo: 1 }", "UNSUPPORTED_PARAMETER")]
    public void RejectsInvalidOrUnsupportedField(string field, string code)
    {
        var result = parser.Parse(Document(field));
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Specification, Is.Null);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(code));
    }

    [TestCase("tables: {}", "EMPTY_TABLES")]
    [TestCase("tables: { users: [] }", "TABLE_NOT_MAPPING")]
    [TestCase("tables: { users: {} }", "MISSING_FIELDS")]
    [TestCase("tables: { users: { fields: [] } }", "FIELDS_NOT_MAPPING")]
    [TestCase("tables: { users: { fields: {} } }", "EMPTY_FIELDS")]
    [TestCase("tables: { users: { fields: { true: { type: boolean } } } }", "INVALID_NAME")]
    public void RejectsInvalidOrUnsupportedTable(string body, string code)
    {
        var result = parser.Parse("spec_version: '1.0'\n" + body);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(code));
    }

    [Test]
    public void DuplicateFieldsAreReportedWithoutThrowing()
    {
        var result = parser.Parse("spec_version: '1.0'\ntables: { users: { fields: { x: { type: integer }, x: { type: string } } } }");
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics, Is.Not.Empty);
    }

    [Test]
    public void ReusesStructureDiagnostics()
    {
        var result = parser.Parse("spec_version: '2.0'\ntables: {}\n");
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("UNSUPPORTED_VERSION"));
    }

    [Test]
    public void ErrorContainsFieldPathAndPosition()
    {
        var result = parser.Parse("spec_version: '1.0'\ntables:\n  users:\n    fields:\n      age: { type: integer, min: bad }");
        var error = result.Diagnostics.Single();
        Assert.That(error.Path, Is.EqualTo("$.tables['users'].fields['age'].min"));
        Assert.That(error.Line, Is.EqualTo(5));
        Assert.That(error.Column, Is.GreaterThan(1));
    }

    private static string Document(string field) => "spec_version: '1.0'\ntables: { items: { fields: { value: " + field + " } } }";
}
