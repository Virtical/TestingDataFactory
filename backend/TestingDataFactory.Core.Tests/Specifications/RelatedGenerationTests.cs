using NUnit.Framework;
using TestingDataFactory.Core.Specifications;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Tests.Specifications;

public class RelatedGenerationTests
{
    private readonly SpecificationParser parser = new();

    [Test]
    public void GeneratesDocumentedRelatedTablesWithDerivedValues()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Specifications", "Examples", "example-2.yaml");
        var parsed = parser.Parse(File.ReadAllText(path));
        Assert.That(parsed.IsSuccess, Is.True, string.Join("; ", parsed.Diagnostics));
        var result = parser.Generate(parsed.Specification!, new GenerationOverrides(Rows: new Dictionary<string, int> { ["customers"] = 10 }));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        var rows = result.Rows!.ToArray();
        var customers = rows.Where(r => r.Table == "customers").ToArray();
        var orders = rows.Where(r => r.Table == "orders").ToArray();
        var items = rows.Where(r => r.Table == "order_items").ToArray();
        Assert.That(customers, Has.Length.EqualTo(10));
        Assert.That(orders, Is.Not.Empty);
        Assert.That(items, Is.Not.Empty);
        var ids = customers.Select(r => r.Values["id"]).ToHashSet();
        var orderIds = orders.Select(r => r.Values["id"]).ToHashSet();
        Assert.That(orders.All(r => ids.Contains(r.Values["customer_id"])), Is.True);
        Assert.That(items.All(r => orderIds.Contains(r.Values["order_id"])), Is.True);
        Assert.That(items.All(r => (decimal)r.Values["amount"]! == (decimal)r.Values["quantity"]! * (decimal)r.Values["price"]!), Is.True);
        Assert.That(customers.All(c => orders.Count(o => Equals(o.Values["customer_id"], c.Values["id"])) <= 8), Is.True);
        Assert.That(orders.All(o => items.Count(i => Equals(i.Values["order_id"], o.Values["id"])) is >= 1 and <= 5), Is.True);
        var replay = parser.Generate(parsed.Specification!, new GenerationOverrides(Rows: new Dictionary<string, int> { ["customers"] = 10 }));
        Assert.That(rows.Select(r => r.Values.Values.ToArray()), Is.EqualTo(replay.Rows!.Select(r => r.Values.Values.ToArray())));
    }

    [Test]
    public void GeneratesFullDocumentedWorkload()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "Specifications", "Examples", "example-2.yaml");
        var parsed = parser.Parse(File.ReadAllText(path));
        var result = parser.Generate(parsed.Specification!);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        var counts = result.Rows!.GroupBy(r => r.Table).ToDictionary(g => g.Key, g => g.Count());
        Assert.That(counts["customers"], Is.EqualTo(500));
        Assert.That(counts["orders"], Is.GreaterThan(0));
        Assert.That(counts["order_items"], Is.GreaterThanOrEqualTo(counts["orders"]));
    }

    [Test]
    public void SupportsUniformDecimalAndLognormal()
    {
        var parsed = parser.Parse("spec_version: '1.0'\ntables: { t: { fields: { x: { type: decimal, min: -1, max: 1, scale: 2 }, y: { type: decimal, min: 100, max: 90000, scale: 2, distribution: lognormal } } } }");
        var result = parser.Generate(parsed.Specification!, new GenerationOverrides(Seed: 7, Rows: new Dictionary<string, int> { ["t"] = 500 }));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        Assert.That(result.Options!.Output, Is.TypeOf<CsvOutput>());
        foreach (var row in result.Rows!)
        {
            var x = (decimal)row.Values["x"]!; var y = (decimal)row.Values["y"]!;
            Assert.That(x, Is.InRange(-1m, 1m)); Assert.That(decimal.Round(x, 2), Is.EqualTo(x));
            Assert.That(y, Is.InRange(100m, 90000m)); Assert.That(decimal.Round(y, 2), Is.EqualTo(y));
        }
    }

    [Test]
    public async Task WritesRelatedTablesToSeparateCsvFiles()
    {
        var path = Path.Combine(Path.GetTempPath(), "testing-data-factory-dir-" + Guid.NewGuid().ToString("N"));
        try
        {
            var example = Path.Combine(TestContext.CurrentContext.TestDirectory, "Specifications", "Examples", "example-2.yaml");
            var parsed = parser.Parse(await File.ReadAllTextAsync(example));
            var result = parser.Generate(parsed.Specification!, new GenerationOverrides(Rows: new Dictionary<string, int> { ["customers"] = 10 }));
            Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
            await result.WriteCsvDirectoryAsync(path);
            Assert.That(Directory.GetFiles(path).Select(Path.GetFileName), Is.EquivalentTo(new[] { "0001-customers.csv", "0002-orders.csv", "0003-order_items.csv" }));
            Assert.That((await File.ReadAllLinesAsync(Path.Combine(path, "0001-customers.csv"))).Length, Is.EqualTo(11));
            Assert.That((await File.ReadAllLinesAsync(Path.Combine(path, "0003-order_items.csv")))[0], Is.EqualTo("id,order_id,product,quantity,price,amount"));
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }

    [Test]
    public void ComputesDerivedInDependencyOrderAndUsesIntegerDivision()
    {
        var parsed = parser.Parse("spec_version: '1.0'\ntables: { t: { fields: { a: { type: integer, derived: 'b + 1' }, b: { type: integer, derived: 'x / 2' }, x: { type: integer, min: 5, max: 5 } } } }");
        var result = parser.Generate(parsed.Specification!, new GenerationOverrides(Rows: new Dictionary<string, int> { ["t"] = 2 }, Seed: 1));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        foreach (var row in result.Rows!)
        {
            Assert.That(row.Values["b"], Is.EqualTo(2m));
            Assert.That(row.Values["a"], Is.EqualTo(3m));
        }
    }

    [Test]
    public void ReturnsErrorForMultipleParentsBeforeStreaming()
    {
        var parsed = parser.Parse("spec_version: '1.0'\ntables: { a: { fields: { id: { type: integer } } }, b: { fields: { id: { type: integer } } }, c: { fields: { a: { ref: a.id }, b: { ref: b.id } } } }");
        var result = parser.Generate(parsed.Specification!, new GenerationOverrides(Rows: new Dictionary<string, int> { ["a"] = 1, ["b"] = 1 }));
        Assert.That(result.Rows, Is.Null);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("UNSUPPORTED_MULTIPLE_PARENTS"));
    }

    [Test]
    public void SupportsDecimalLatticeBoundariesAndRejectsImpossiblePrecision()
    {
        var parsed = parser.Parse("spec_version: '1.0'\ntables: { t: { fields: { x: { type: decimal, min: -0.02, max: 0.02, scale: 2, unique: true } } } }");
        var result = parser.Generate(parsed.Specification!, new GenerationOverrides(Seed: 4, Rows: new Dictionary<string, int> { ["t"] = 5 }));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        Assert.That(result.Rows!.Select(r => r.Values["x"]), Is.EquivalentTo(new object[] { -0.02m, -0.01m, 0m, 0.01m, 0.02m }));
        var invalid = parser.Parse("spec_version: '1.0'\ntables: { t: { fields: { x: { type: decimal, min: 0.001, max: 0.009, scale: 2 } } } }");
        var failure = parser.Generate(invalid.Specification!, new GenerationOverrides(Rows: new Dictionary<string, int> { ["t"] = 1 }));
        Assert.That(failure.Rows, Is.Null);
        Assert.That(failure.Diagnostics.Select(d => d.Code), Does.Contain("UNSUPPORTED_DECIMAL_RANGE"));
    }

    [Test]
    public async Task DirectoryIsNotPublishedWhenDerivedFails()
    {
        var path = Path.Combine(Path.GetTempPath(), "testing-data-factory-failure-" + Guid.NewGuid().ToString("N"));
        try
        {
            var parsed = parser.Parse("spec_version: '1.0'\ntables: { t: { fields: { x: { type: integer, min: 1, max: 1 }, y: { type: integer, derived: 'x / 0' } } } }\ngeneration: { rows: { t: 1 }, output: { csv: {} } }");
            var result = parser.Generate(parsed.Specification!);
            Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await result.WriteCsvDirectoryAsync(path));
            Assert.That(Directory.Exists(path), Is.False);
            Assert.That(Directory.EnumerateDirectories(Path.GetTempPath(), "." + Path.GetFileName(path) + ".*.tmp"), Is.Empty);
            await Task.CompletedTask;
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }

    [Test]
    public void RejectsSqlOutputBeforeGeneration()
    {
        var parsed = parser.Parse("spec_version: '1.0'\ntables: { t: { fields: { id: { type: integer } } } }\ngeneration: { rows: { t: 1 }, output: { sql: { dialect: postgresql, style: script } } }");
        Assert.That(parsed.IsSuccess, Is.True, string.Join("; ", parsed.Diagnostics));
        var result = parser.Generate(parsed.Specification!);
        Assert.That(result.Rows, Is.Null);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("UNSUPPORTED_OUTPUT"));
    }

    [Test]
    public void RejectsUnsupportedDerivedBeforeStreaming()
    {
        var parsed = parser.Parse("spec_version: '1.0'\ntables: { t: { fields: { x: { generator: person.full_name }, y: { type: boolean, derived: \"x.matches('.*')\" } } } }");
        Assert.That(parsed.IsSuccess, Is.True, string.Join("; ", parsed.Diagnostics));
        var result = parser.Generate(parsed.Specification!, new GenerationOverrides(Rows: new Dictionary<string, int> { ["t"] = 1 }));
        Assert.That(result.Rows, Is.Null);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("UNSUPPORTED_DERIVED"));
    }

    [Test]
    public void RejectsNullableParentAndTooManyChildrenForUniqueField()
    {
        var nullable = parser.Parse("spec_version: '1.0'\ntables: { a: { fields: { id: { type: integer, null_rate: 0.5 } } }, b: { fields: { a: { ref: a.id } } } }");
        var rejected = parser.Generate(nullable.Specification!, new GenerationOverrides(Rows: new Dictionary<string, int> { ["a"] = 1 }));
        Assert.That(rejected.Diagnostics.Select(d => d.Code), Does.Contain("UNSUPPORTED_NULLABLE_PARENT"));

        var finite = parser.Parse("spec_version: '1.0'\ntables: { a: { fields: { id: { type: integer, min: 1, max: 1 } } }, b: { fields: { a: { ref: a.id, per_parent: { min: 2, max: 2 } }, id: { type: integer, min: 1, max: 1, unique: true } } } }");
        var result = parser.Generate(finite.Specification!, new GenerationOverrides(Rows: new Dictionary<string, int> { ["a"] = 1 }, Seed: 1));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics));
        Assert.Throws<InvalidOperationException>(() => result.Rows!.ToArray());
    }

    [Test]
    public async Task DoesNotOverwriteExistingDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "testing-data-factory-existing-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(path);
            var marker = Path.Combine(path, "existing.txt");
            await File.WriteAllTextAsync(marker, "unchanged");
            var parsed = parser.Parse("spec_version: '1.0'\ntables: { t: { fields: { x: { type: integer } } } }\ngeneration: { rows: { t: 1 }, output: { csv: {} } }");
            var result = parser.Generate(parsed.Specification!);
            Assert.ThrowsAsync<IOException>(async () => await result.WriteCsvDirectoryAsync(path));
            Assert.That(await File.ReadAllTextAsync(marker), Is.EqualTo("unchanged"));
            Assert.That(Directory.EnumerateDirectories(Path.GetTempPath(), "." + Path.GetFileName(path) + ".*.tmp"), Is.Empty);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
}
