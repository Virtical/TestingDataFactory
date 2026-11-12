using NUnit.Framework;
using TestingDataFactory.Core.Specifications.Model;
using TestingDataFactory.Core.Specifications.Parsing;

namespace TestingDataFactory.Core.Tests.Specifications;

// Примеры 1–3 из docs/Спецификация.docx. YAML хранится отдельно, чтобы его было удобно сверять с документом.
public class DocumentedSpecificationExamplesTests
{
    private readonly YamlSpecificationParser parser = new();

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void DataValidationParsesDocumentedExampleIgnoringGeneration(int number)
    {
        var result = parser.Parse(ReadExample(number), SpecificationParsePurpose.DataValidation);
        Assert.That(result.IsSuccess, Is.True,
            string.Join("; ", result.Diagnostics.Select(d => $"{d.Code}: {d.Path}: {d.Message}")));
        Assert.That(result.Specification!.Generation, Is.Null);
    }

    [Test]
    public void ParsesSingleTableExample()
    {
        var spec = ParseExample(1);
        var fields = spec.Tables["users"].Fields;
        Assert.Multiple(() =>
        {
            Assert.That(spec.Version, Is.EqualTo("1.0"));
            Assert.That(spec.Locale, Is.EqualTo("ru_RU"));
            Assert.That(spec.Tables.Keys, Is.EquivalentTo(new[] { "users" }));
            Assert.That(((DictionaryEnumDefinition)spec.Types["city"]).Dictionary, Is.EqualTo("russian_cities"));
            Assert.That(fields["id"].Primary && fields["id"].Unique, Is.True);
            Assert.That(fields["full_name"].Generator, Is.EqualTo("person.full_name"));
            Assert.That(fields["email"].Unique && fields["email"].Pii, Is.True);
            Assert.That(fields["age"].Distribution, Is.EqualTo(new NormalDistribution(35, 12)));
            Assert.That(fields["is_active"].TrueRate, Is.EqualTo(0.85m));
            Assert.That(fields["phone"].NullRate, Is.EqualTo(0.1m));
            Assert.That(spec.Generation!.Rows["users"], Is.EqualTo(1000));
            Assert.That(spec.Generation.Seed, Is.EqualTo(42));
            Assert.That(spec.Generation.Output, Is.EqualTo(new CsvOutput()));
        });
    }

    [Test]
    public void ParsesRelatedTablesExample()
    {
        var spec = ParseExample(2);
        var order = spec.Tables["orders"].Fields["customer_id"].Reference!;
        var item = spec.Tables["order_items"].Fields["order_id"].Reference!;
        var amount = spec.Tables["order_items"].Fields["amount"];
        Assert.Multiple(() =>
        {
            Assert.That(spec.TableOrder, Is.EqualTo(new[] { "customers", "orders", "order_items" }));
            Assert.That(order, Is.EqualTo(new FieldReference("customers", "id", new PerParentRange(0, 8))));
            Assert.That(item, Is.EqualTo(new FieldReference("orders", "id", new PerParentRange(1, 5))));
            Assert.That(amount.Derived!.Source, Is.EqualTo("quantity * price"));
            Assert.That(amount.Derived.Dependencies, Is.EquivalentTo(new[] { "quantity", "price" }));
            Assert.That(spec.Tables["order_items"].Fields["price"].Distribution, Is.TypeOf<LogNormalDistribution>());
            Assert.That(spec.Generation!.Rows["customers"], Is.EqualTo(500));
            Assert.That(spec.Generation.Output, Is.EqualTo(new SqlOutput(SqlDialect.PostgreSql, SqlOutputStyle.Script)));
        });
    }

    [Test]
    public void ParsesDictionaryAndRulesExample()
    {
        var spec = ParseExample(3);
        var geo = (DictionaryEnumDefinition)spec.Types["geo"];
        var accounts = spec.Tables["accounts"].Fields;
        var subscriptions = spec.Tables["subscriptions"];
        Assert.Multiple(() =>
        {
            Assert.That(geo.Dictionary, Is.EqualTo("world_cities"));
            Assert.That(geo.Where!.Source, Is.EqualTo("country in ['RU', 'KZ', 'BY']"));
            Assert.That(new[] { accounts["country"].Column, accounts["city"].Column, accounts["currency"].Column },
                Is.EqualTo(new[] { "country", "city", "currency" }));
            Assert.That(subscriptions.Rules, Has.Count.EqualTo(5));
            Assert.That(subscriptions.Rules[1].When!.Source, Is.EqualTo("status == 'cancelled'"));
            Assert.That(subscriptions.Rules[4].Severity, Is.EqualTo(RuleSeverity.Warning));
            Assert.That(subscriptions.Fields["final_price"].Derived!.Dependencies,
                Is.EquivalentTo(new[] { "base_price", "discount_pct" }));
            Assert.That(spec.TableOrder, Is.EqualTo(new[] { "accounts", "subscriptions", "invoices" }));
            Assert.That(spec.Generation!.Mode, Is.EqualTo(GenerationMode.Anomalies));
            Assert.That(spec.Generation.Output, Is.TypeOf<JsonOutput>());
        });
    }

    [TestCase("spec_version: '2.0'\ntables: {}", "UNSUPPORTED_VERSION", "$.\u0027spec_version\u0027")]
    [TestCase("spec_version: '1.0'\ntables: { t: { fields: { x: { type: integer, min: 5, max: 2 } } } }", "INVALID_RANGE", "$.tables['t'].fields['x']")]
    [TestCase("spec_version: '1.0'\ntables: { t: { fields: { x: { ref: absent.id } } } }", "UNKNOWN_REFERENCE_TABLE", "$.tables['t'].fields['x'].ref")]
    [TestCase("spec_version: '1.0'\ntables: { t: { fields: { x: { type: integer } }, rules: [{ expr: 'missing > 0' }] } }", "INVALID_CEL", "$.tables['t'].rules[0].expr")]
    [TestCase("spec_version: '1.0'\ntables: { t: { fields: { x: { type: integer } } } }\ngeneration: { output: { csv: {}, json: {} } }", "INVALID_OUTPUT", "$.generation.output")]
    public void RejectsInvalidSpecificationWithDiagnostic(string yaml, string code, string path)
    {
        var result = parser.Parse(yaml);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Specification, Is.Null);
        Assert.That(result.Diagnostics.Any(d => d.Code == code && d.Path == path), Is.True,
            string.Join("; ", result.Diagnostics.Select(d => $"{d.Code}: {d.Path}: {d.Message}")));
    }

    private Specification ParseExample(int number)
    {
        var result = parser.Parse(ReadExample(number));
        Assert.That(result.IsSuccess, Is.True,
            string.Join("; ", result.Diagnostics.Select(d => $"{d.Code}: {d.Path}: {d.Message}")));
        return result.Specification!;
    }

    private static string ReadExample(int number) => File.ReadAllText(Path.Combine(
        TestContext.CurrentContext.TestDirectory, "Specifications", "Examples", $"example-{number}.yaml"));
}
