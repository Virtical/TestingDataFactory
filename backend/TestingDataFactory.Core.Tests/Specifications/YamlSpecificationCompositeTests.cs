using NUnit.Framework;
using TestingDataFactory.Core.Specifications.Model;
using TestingDataFactory.Core.Specifications.Parsing;

namespace TestingDataFactory.Core.Tests.Specifications;

public class YamlSpecificationCompositeTests
{
    private readonly YamlSpecificationParser parser = new();

    [Test]
    public void ParsesDocumentedObjectAndArrayExample()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            types:
              city: { enum: { dictionary: russian_cities } }
              address:
                type: object
                fields:
                  city:   { type: city }
                  street: { type: string, max_length: 50 }
            tables:
              users:
                fields:
                  address: { type: address }
                  tags:    { type: array, items: { type: string, max_length: 10 }, min_items: 1, max_items: 5 }
            """);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        var fields = result.Specification!.Tables["users"].Fields;
        Assert.Multiple(() =>
        {
            Assert.That(fields["address"].Type, Is.EqualTo(FieldType.Object));
            Assert.That(fields["address"].NamedType, Is.EqualTo("address"));
            Assert.That(fields["address"].Fields!["city"].NamedType, Is.EqualTo("city"));
            Assert.That(fields["address"].Fields!["street"].MaxLength, Is.EqualTo(50));
            Assert.That(fields["tags"].Type, Is.EqualTo(FieldType.Array));
            Assert.That(fields["tags"].Items!.MaxLength, Is.EqualTo(10));
            Assert.That(fields["tags"].MinItems, Is.EqualTo(1));
            Assert.That(fields["tags"].MaxItems, Is.EqualTo(5));
        });
    }

    [Test]
    public void ResolvesNestedNamedTypesAndArrayOverrides()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            locale: en_US
            types:
              group: { type: array, items: { type: person }, min_items: 1, max_items: 9 }
              person: { type: object, fields: { names: { type: array, items: { type: string, locale: ru_RU } } } }
              small_group: { type: group, max_items: 3 }
            tables:
              t: { fields: { people: { type: small_group, min_items: 2 } } }
            """);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        var people = result.Specification!.Tables["t"].Fields["people"];
        Assert.Multiple(() =>
        {
            Assert.That(people.MinItems, Is.EqualTo(2));
            Assert.That(people.MaxItems, Is.EqualTo(3));
            Assert.That(people.Items!.Type, Is.EqualTo(FieldType.Object));
            Assert.That(people.Items.Locale, Is.EqualTo("en_US"));
            Assert.That(people.Items.Fields!["names"].Items!.Locale, Is.EqualTo("ru_RU"));
        });
    }

    [TestCase("{ type: object }", "MISSING_FIELDS", "$.tables['t'].fields['value'].fields")]
    [TestCase("{ type: object, fields: [] }", "FIELDS_NOT_MAPPING", "$.tables['t'].fields['value'].fields")]
    [TestCase("{ type: object, fields: {} }", "EMPTY_FIELDS", "$.tables['t'].fields['value'].fields")]
    [TestCase("{ type: array }", "MISSING_ITEMS", "$.tables['t'].fields['value'].items")]
    [TestCase("{ type: array, items: [] }", "FIELD_NOT_MAPPING", "$.tables['t'].fields['value'].items")]
    [TestCase("{ type: array, items: { type: string }, min_items: 4, max_items: 2 }", "INVALID_RANGE", "$.tables['t'].fields['value']")]
    [TestCase("{ type: array, items: { type: string }, min_items: -1 }", "INVALID_PARAMETER", "$.tables['t'].fields['value'].min_items")]
    [TestCase("{ type: integer, items: { type: string } }", "INCOMPATIBLE_PARAMETER", "$.tables['t'].fields['value'].items")]
    [TestCase("{ type: array, items: { type: object, fields: { age: { type: integer, min: nope } } } }", "INVALID_PARAMETER", "$.tables['t'].fields['value'].items.fields['age'].min")]
    [TestCase("{ type: object, fields: { x: { ref: t.id } } }", "UNSUPPORTED_NESTED_PARAMETER", "$.tables['t'].fields['value'].fields['x'].ref")]
    [TestCase("{ type: object, fields: { x: { type: string, primary: true } } }", "UNSUPPORTED_NESTED_PARAMETER", "$.tables['t'].fields['value'].fields['x'].primary")]
    [TestCase("{ type: object, fields: { x: { type: string, unique: false } } }", "UNSUPPORTED_NESTED_PARAMETER", "$.tables['t'].fields['value'].fields['x'].unique")]
    [TestCase("{ type: array, items: { type: integer, derived: '1' } }", "UNSUPPORTED_NESTED_PARAMETER", "$.tables['t'].fields['value'].items.derived")]
    public void RejectsInvalidCompositeWithPrecisePath(string field, string code, string path)
    {
        var result = parser.Parse("spec_version: '1.0'\ntables: { t: { fields: { value: " + field + " } } }");
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Any(d => d.Code == code && d.Path == path && d.Line is > 0 && d.Column is > 0), Is.True,
            string.Join("; ", result.Diagnostics));
    }

    [Test]
    public void RejectsRecursiveNamedComposite()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            types:
              a: { type: object, fields: { next: { type: b } } }
              b: { type: array, items: { type: a } }
            tables: { t: { fields: { x: { type: a } } } }
            """);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("TYPE_CYCLE"));
    }

    [Test]
    public void RejectsInconsistentInheritedArrayBounds()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            types: { tags: { type: array, items: { type: string }, max_items: 3 } }
            tables: { t: { fields: { x: { type: tags, min_items: 4 } } } }
            """);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INVALID_RANGE"));
    }

    [TestCase("object", "{ type: object, fields: { id: { type: integer } } }")]
    [TestCase("array", "{ type: array, items: { type: integer } }")]
    public void RejectsReferenceToCompositeField(string type, string target)
    {
        var yaml = "spec_version: '1.0'\ntables: { parent: { fields: { value: " + target + " } }, child: { fields: { value: { ref: parent.value } } } }";
        var result = parser.Parse(yaml);
        Assert.That(result.IsSuccess, Is.False, type);
        Assert.That(result.Diagnostics.Any(d => d.Code == "UNSUPPORTED_COMPOSITE_PARAMETER" && d.Path == "$.tables['child'].fields['value'].ref"), Is.True,
            string.Join("; ", result.Diagnostics));
    }

    [TestCase("object", "primary: true")]
    [TestCase("object", "unique: false")]
    [TestCase("object", "derived: 'null'")]
    [TestCase("array", "primary: false")]
    [TestCase("array", "unique: true")]
    [TestCase("array", "derived: 'null'")]
    public void RejectsParametersOnNamedCompositeField(string type, string parameter)
    {
        var definition = type == "object" ? "{ type: object, fields: { x: { type: string } } }" : "{ type: array, items: { type: string } }";
        var yaml = "spec_version: '1.0'\ntypes: { composite: " + definition + " }\ntables: { t: { fields: { x: { type: composite, " + parameter + " } } } }";
        var result = parser.Parse(yaml);
        var key = parameter.Split(':')[0];
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Any(d => d.Code == "UNSUPPORTED_COMPOSITE_PARAMETER" && d.Path == "$.tables['t'].fields['x']." + key), Is.True,
            string.Join("; ", result.Diagnostics));
    }

    [Test]
    public void RejectsRedefinitionOfNamedCompositeShape()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            types: { tags: { type: array, items: { type: string } } }
            tables: { t: { fields: { x: { type: tags, items: { type: integer } } } } }
            """);
        Assert.That(result.Diagnostics.Any(d => d.Code == "UNSUPPORTED_PARAMETER" && d.Path == "$.tables['t'].fields['x'].items"), Is.True);
    }
}
