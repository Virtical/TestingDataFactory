using NUnit.Framework;
using TestingDataFactory.Core.Specifications.Model;
using TestingDataFactory.Core.Specifications.Parsing;

namespace TestingDataFactory.Core.Tests.Specifications;

public class YamlSpecificationTypesTests
{
    private readonly YamlSpecificationParser parser = new();

    [Test]
    public void ResolvesPrimitiveTypesAndFieldOverrides()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            types:
              price: { type: money, max: 100 }
              money: { type: decimal, min: 0, scale: 2 }
              inn_ru: { type: regex, pattern: '^\d{10}$' }
            tables:
              items:
                fields:
                  unit_price: { type: price, min: 5 }
                  inn: { type: inn_ru }
            """);

        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var spec = result.Specification!;
        Assert.That(spec.Types.Keys, Is.EquivalentTo(new[] { "price", "money", "inn_ru" }));
        var field = spec.Tables["items"].Fields["unit_price"];
        Assert.That(field.Type, Is.EqualTo(FieldType.Decimal));
        Assert.That(field.NamedType, Is.EqualTo("price"));
        Assert.That(field.Min, Is.EqualTo(5));
        Assert.That(field.Max, Is.EqualTo(100));
        Assert.That(field.Scale, Is.EqualTo(2));
        Assert.That(spec.Tables["items"].Fields["inn"].Pattern, Is.EqualTo("^\\d{10}$"));
    }

    [Test]
    public void ResolvesSimpleWeightedAndMulticolumnEnums()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            types:
              product: { enum: [Ноутбук, Мышь] }
              status: { enum: { new: 2, paid: 6, shipped: 2 } }
              university:
                enum:
                  columns: [name, city]
                  rows:
                    - [МГУ, Москва]
                    - [СПбГУ, Санкт-Петербург]
            tables:
              students:
                fields:
                  item: { type: product }
                  state: { type: status }
                  university: { type: university, column: name }
                  city: { type: university, column: city }
            """);

        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var types = result.Specification!.Types;
        Assert.That(((ValuesEnumDefinition)types["product"]).Values.Select(v => v.Value), Is.EqualTo(new[] { "Ноутбук", "Мышь" }));
        Assert.That(((ValuesEnumDefinition)types["status"]).Values.Select(v => v.Weight), Is.EqualTo(new decimal?[] { 2, 6, 2 }));
        var university = (TableEnumDefinition)types["university"];
        Assert.That(university.Columns, Is.EqualTo(new[] { "name", "city" }));
        Assert.That(university.Rows[0], Is.EqualTo(new[] { "МГУ", "Москва" }));
        var city = result.Specification.Tables["students"].Fields["city"];
        Assert.That(city.Type, Is.EqualTo(FieldType.Enum));
        Assert.That(city.NamedType, Is.EqualTo("university"));
        Assert.That(city.Column, Is.EqualTo("city"));
    }

    [Test]
    public void ResolvesBuiltInDictionaries()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            types:
              city: { enum: { dictionary: russian_cities } }
              geo: { enum: { dictionary: world_cities } }
            tables:
              locations:
                fields:
                  place: { type: city }
                  country: { type: geo, column: country }
            """);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.That(((DictionaryEnumDefinition)result.Specification!.Types["geo"]).Columns, Does.Contain("country"));
    }

    [Test]
    public void SupportsExternalDictionaryMetadata()
    {
        var custom = new YamlSpecificationParser(new TestRegistry());
        var result = custom.Parse(Document("custom: { enum: { dictionary: organizations } }", "{ type: custom, column: name }"));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
    }

    [TestCase("a: { type: b }\n  b: { type: a }", "{ type: a }", "TYPE_CYCLE")]
    [TestCase("a: { type: missing }", "{ type: a }", "INVALID_FIELD_TYPE")]
    [TestCase("integer: { type: decimal }", "{ type: integer }", "RESERVED_TYPE_NAME")]
    [TestCase("a: { type: integer, min: 2, max: 1 }", "{ type: integer }", "INVALID_RANGE")]
    [TestCase("a: { type: integer, primary: true }", "{ type: integer }", "UNSUPPORTED_PARAMETER")]
    [TestCase("a: { enum: [] }", "{ type: a }", "EMPTY_ENUM")]
    [TestCase("a: { enum: [x, x] }", "{ type: a }", "DUPLICATE_ENUM_VALUE")]
    [TestCase("a: { enum: { x: -1, y: 2 } }", "{ type: a }", "INVALID_WEIGHT")]
    [TestCase("a: { enum: { x: 0, y: 0 } }", "{ type: a }", "INVALID_WEIGHT")]
    [TestCase("a: { enum: { dictionary: nonexistent } }", "{ type: a }", "UNKNOWN_DICTIONARY")]
    [TestCase("a: { enum: { columns: [x, x], rows: [[a, b]] } }", "{ type: a, column: x }", "INVALID_ENUM_TABLE")]
    [TestCase("a: { enum: { columns: [x, y], rows: [[a]] } }", "{ type: a, column: x }", "INVALID_ENUM_ROW")]
    [TestCase("a: { enum: { columns: [x, y], rows: [[a, b]] } }", "{ type: a }", "MISSING_COLUMN")]
    [TestCase("a: { enum: { columns: [x, y], rows: [[a, b]] } }", "{ type: a, column: z }", "INVALID_COLUMN")]
    [TestCase("a: { enum: [x, y] }", "{ type: a, column: x }", "INVALID_COLUMN")]
    public void RejectsInvalidTypesAndReferences(string types, string field, string code)
    {
        var result = parser.Parse(Document(types, field));
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Specification, Is.Null);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(code));
    }

    [Test]
    public void ValidatesInheritedRangeAfterFieldOverride()
    {
        var result = parser.Parse(Document("money: { type: decimal, min: 0, max: 100 }", "{ type: money, min: 200 }"));
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INVALID_RANGE"));
    }

    [Test]
    public void RejectsExcessiveTypeResolutionDepth()
    {
        var declarations = string.Join("\n  ", Enumerable.Range(0, 70).Select(i => $"t{i}: {{ type: {(i == 69 ? "integer" : $"t{i + 1}")} }}"));
        var result = parser.Parse(Document(declarations, "{ type: t0 }"));
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("TYPE_DEPTH_EXCEEDED"));
    }

    private static string Document(string types, string field) =>
        "spec_version: '1.0'\ntypes:\n  " + types + "\ntables: { t: { fields: { value: " + field + " } } }";

    private sealed class TestRegistry : ISpecificationDictionaryRegistry
    {
        public bool TryGetColumns(string name, out IReadOnlyList<string> columns)
        {
            columns = new[] { "name", "city" };
            return name == "organizations";
        }
    }
}
