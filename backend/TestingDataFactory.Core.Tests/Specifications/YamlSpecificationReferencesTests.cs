using NUnit.Framework;
using TestingDataFactory.Core.Specifications.Model;
using TestingDataFactory.Core.Specifications.Parsing;

namespace TestingDataFactory.Core.Tests.Specifications;

public class YamlSpecificationReferencesTests
{
    private readonly YamlSpecificationParser parser = new();

    [Test]
    public void ResolvesForwardReferencesAndOrdersTables()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            types:
              key: { type: integer, min: 1 }
            tables:
              order_items:
                fields:
                  order_id: { ref: orders.id, per_parent: { min: 1, max: 5 } }
              orders:
                fields:
                  id: { type: key, primary: true }
                  customer_id: { ref: customers.id, per_parent: { min: 0, max: 8 } }
              customers:
                fields:
                  id: { type: uuid, primary: true }
            """);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var spec = result.Specification!;
        Assert.That(spec.TableOrder, Is.EqualTo(new[] { "customers", "orders", "order_items" }));
        var field = spec.Tables["order_items"].Fields["order_id"];
        Assert.That(field.Type, Is.EqualTo(FieldType.Integer));
        Assert.That(field.NamedType, Is.EqualTo("key"));
        Assert.That(field.Reference, Is.EqualTo(new FieldReference("orders", "id", new PerParentRange(1, 5))));
        Assert.That(spec.Tables["orders"].Fields["customer_id"].Type, Is.EqualTo(FieldType.Uuid));
    }

    [Test]
    public void ResolvesReferenceChainsAndDoesNotCopyTargetFieldFlags()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              c: { fields: { id: { ref: b.id } } }
              b: { fields: { id: { ref: a.id, unique: true } } }
              a: { fields: { id: { type: uuid, primary: true } } }
            """);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var field = result.Specification!.Tables["c"].Fields["id"];
        Assert.That(field.Type, Is.EqualTo(FieldType.Uuid));
        Assert.That(field.Unique, Is.False);
        Assert.That(field.Primary, Is.False);
    }

    [Test]
    public void DoesNotCountMultipleReferencesToSameTableAsMultipleDependencies()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              a: { fields: { id: { type: integer } } }
              b: { fields: { first: { ref: a.id }, second: { ref: a.id } } }
            """);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Specification!.TableOrder, Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void ReferenceKeepsNamedTypeAndColumnOfTarget()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            types:
              geo: { enum: { columns: [country, city], rows: [[RU, Moscow]] } }
            tables:
              child: { fields: { country: { ref: parent.country } } }
              parent: { fields: { country: { type: geo, column: country } } }
            """);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var field = result.Specification!.Tables["child"].Fields["country"];
        Assert.That(field.Type, Is.EqualTo(FieldType.Enum));
        Assert.That(field.NamedType, Is.EqualTo("geo"));
        Assert.That(field.Column, Is.EqualTo("country"));
    }

    [Test]
    public void OrdersIndependentTablesDeterministically()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              child_a: { fields: { id: { ref: parent.id } } }
              parent: { fields: { id: { type: integer } } }
              child_b: { fields: { id: { ref: parent.id } } }
              independent: { fields: { id: { type: integer } } }
            """);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Specification!.TableOrder, Is.EqualTo(new[] { "parent", "independent", "child_a", "child_b" }));
    }

    [TestCase("{ ref: absent.id }", "UNKNOWN_REFERENCE_TABLE")]
    [TestCase("{ ref: parent.absent }", "UNKNOWN_REFERENCE_FIELD")]
    [TestCase("{ ref: parent }", "INVALID_REFERENCE")]
    [TestCase("{ ref: parent.id.extra }", "INVALID_REFERENCE")]
    [TestCase("{ ref: '' }", "INVALID_REFERENCE")]
    [TestCase("{ ref: 23 }", "INVALID_REFERENCE")]
    [TestCase("{ ref: parent.id, type: integer }", "UNSUPPORTED_PARAMETER")]
    [TestCase("{ ref: parent.id, per_parent: [] }", "INVALID_PER_PARENT")]
    [TestCase("{ ref: parent.id, per_parent: { min: 0 } }", "MISSING_PER_PARENT_BOUND")]
    [TestCase("{ ref: parent.id, per_parent: { min: -1, max: 2 } }", "INVALID_PARAMETER")]
    [TestCase("{ ref: parent.id, per_parent: { min: 2, max: 1 } }", "INVALID_RANGE")]
    [TestCase("{ ref: parent.id, per_parent: { min: 0, max: 1, extra: 2 } }", "UNSUPPORTED_PARAMETER")]
    [TestCase("{ ref: parent.id, primary: true, null_rate: 0.5 }", "PRIMARY_NULLABLE")]
    [TestCase("{ type: integer, per_parent: { min: 0, max: 1 } }", "UNSUPPORTED_PARAMETER")]
    public void RejectsInvalidReferenceOrRange(string field, string code)
    {
        var result = parser.Parse("spec_version: '1.0'\ntables: { child: { fields: { value: " + field + " } }, parent: { fields: { id: { type: integer } } } }");
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Specification, Is.Null);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(code));
    }

    [TestCase("tables: { a: { fields: { id: { ref: a.id } } } }")]
    [TestCase("tables: { a: { fields: { id: { ref: b.id } } }, b: { fields: { id: { ref: a.id } } } }")]
    public void RejectsCycles(string tables)
    {
        var result = parser.Parse("spec_version: '1.0'\n" + tables);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("REFERENCE_CYCLE"));
    }

    [Test]
    public void ReportsMultipleBrokenReferencesWithPositions()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            tables:
              a: { fields: { x: { ref: missing.id } } }
              b: { fields: { y: { ref: a.absent } } }
            """);
        Assert.That(result.Diagnostics.Select(d => d.Path), Is.EquivalentTo(new[] { "$.tables['a'].fields['x'].ref", "$.tables['b'].fields['y'].ref" }));
        Assert.That(result.Diagnostics.Select(d => d.Line), Is.EquivalentTo(new int?[] { 3, 4 }));
    }
}
