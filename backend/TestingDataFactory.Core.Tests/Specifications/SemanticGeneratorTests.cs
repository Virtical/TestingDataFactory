using NUnit.Framework;
using TestingDataFactory.Core.Generation;
using TestingDataFactory.Core.Specifications.Model;
using TestingDataFactory.Core.Specifications.Parsing;

namespace TestingDataFactory.Core.Tests.Specifications;

public class SemanticGeneratorTests
{
    private readonly YamlSpecificationParser parser = new();

    [TestCase("person.full_name")]
    [TestCase("person.first_name")]
    [TestCase("person.last_name")]
    [TestCase("internet.email")]
    [TestCase("internet.username")]
    [TestCase("phone")]
    [TestCase("address.city")]
    [TestCase("finance.card_number")]
    public void ParsesAndGeneratesSupportedValues(string name)
    {
        var field = ParseField($"{{ generator: {name} }}");
        Assert.That(field.Type, Is.EqualTo(FieldType.String));
        Assert.That(field.Generator, Is.EqualTo(name));
        Assert.That(field.Locale, Is.EqualTo("ru_RU"));
        var value = new SemanticValueGenerator(field, 42).Next();
        Assert.That(value, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void LocalePriorityIsFieldThenTypeThenDocument()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            locale: en_US
            types:
              russian: { type: string, locale: ru_RU }
              derived: { type: russian }
              status: { enum: [new, paid], locale: ru_RU }
            tables:
              items:
                fields:
                  from_doc: { generator: person.full_name }
                  from_field: { generator: person.full_name, locale: en_GB }
                  from_type: { type: derived }
                  type_override: { type: russian, locale: en_GB }
                  enum_type: { type: status }
            """);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var fields = result.Specification!.Tables["items"].Fields;
        Assert.That(fields["from_doc"].Locale, Is.EqualTo("en_US"));
        Assert.That(fields["from_field"].Locale, Is.EqualTo("en_GB"));
        Assert.That(fields["from_type"].Locale, Is.EqualTo("ru_RU"));
        Assert.That(fields["type_override"].Locale, Is.EqualTo("en_GB"));
        Assert.That(fields["enum_type"].Locale, Is.EqualTo("ru_RU"));
    }

    [Test]
    public void ReferenceLocaleUsesOwnSettingThenNamedTypeThenDocument()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            locale: en_US
            types: { word: { type: string, locale: ru_RU } }
            tables:
              child:
                fields:
                  a: { ref: parent.name }
                  b: { ref: parent.name, locale: en_GB }
              parent:
                fields:
                  name: { type: word, locale: en_US }
            """);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var fields = result.Specification!.Tables["child"].Fields;
        Assert.That(fields["a"].Locale, Is.EqualTo("ru_RU"));
        Assert.That(fields["b"].Locale, Is.EqualTo("en_GB"));
    }

    [TestCase("{ generator: not.real }", "UNKNOWN_GENERATOR")]
    [TestCase("{ generator: person.full_name, type: string }", "UNSUPPORTED_PARAMETER")]
    [TestCase("{ generator: person.full_name, scale: 2 }", "UNSUPPORTED_PARAMETER")]
    [TestCase("{ generator: person.full_name, min_length: 20, max_length: 2 }", "INVALID_RANGE")]
    [TestCase("{ generator: person.full_name, null_rate: 1.1 }", "INVALID_RATE")]
    [TestCase("{ generator: person.full_name, locale: no_such_locale }", "UNSUPPORTED_LOCALE")]
    [TestCase("{ generator: null }", "INVALID_NAME")]
    public void RejectsInvalidGeneratorFields(string declaration, string code)
    {
        var result = parser.Parse(Document(declaration));
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(code));
    }

    [Test]
    public void RejectsUnsupportedDocumentAndTypeLocales()
    {
        var yaml = """
            spec_version: '1.0'
            locale: zz_ZZ
            types: { custom: { type: string, locale: xx_XX } }
            tables: { items: { fields: { value: { type: string } } } }
            """;
        var result = parser.Parse(yaml);
        Assert.That(result.Diagnostics.Where(d => d.Code == "UNSUPPORTED_LOCALE").Select(d => d.Path),
            Is.EquivalentTo(new[] { "$.locale", "$.types['custom'].locale" }));
    }

    [Test]
    public void SameSeedCreatesSameSequenceWithoutSharedRandomState()
    {
        var field = ParseField("{ generator: person.full_name, locale: en_US }");
        var first = new SemanticValueGenerator(field, 123);
        var second = new SemanticValueGenerator(field, 123);
        var different = new SemanticValueGenerator(field, 124);
        var values = Enumerable.Range(0, 10).Select(_ => first.Next()).ToArray();
        Assert.That(Enumerable.Range(0, 10).Select(_ => second.Next()).ToArray(), Is.EqualTo(values));
        Assert.That(Enumerable.Range(0, 10).Select(_ => different.Next()).ToArray(), Is.Not.EqualTo(values));
    }

    [TestCase("person.full_name")]
    [TestCase("person.first_name")]
    [TestCase("person.last_name")]
    [TestCase("internet.email")]
    [TestCase("internet.username")]
    [TestCase("phone")]
    [TestCase("address.city")]
    [TestCase("finance.card_number")]
    public void EveryGeneratorUsesLocalSeed(string name)
    {
        var field = ParseField($"{{ generator: {name} }}");
        var left = new SemanticValueGenerator(field, 42);
        var right = new SemanticValueGenerator(field, 42);
        var first = Enumerable.Range(0, 5).Select(_ => left.Next()).ToArray();
        var second = Enumerable.Range(0, 5).Select(_ => right.Next()).ToArray();
        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void RussianLocaleProducesRussianNames()
    {
        var field = ParseField("{ generator: person.full_name, locale: ru_RU }");
        var generator = new SemanticValueGenerator(field, 123);
        var names = Enumerable.Range(0, 5).Select(_ => generator.Next()).ToArray();
        Assert.That(names.All(n => n!.Any(c => c is >= '\u0400' and <= '\u04ff')), Is.True);
    }

    [Test]
    public void HonorsUniqueNullRateAndLength()
    {
        var field = ParseField("{ generator: internet.email, unique: true, min_length: 4, max_length: 100 }");
        var generator = new SemanticValueGenerator(field, 1);
        var values = Enumerable.Range(0, 50).Select(_ => generator.Next()).ToArray();
        Assert.That(values.Distinct().Count(), Is.EqualTo(50));
        Assert.That(values.All(value => value is { Length: >= 4 and <= 100 }), Is.True);
        var nullField = ParseField("{ generator: phone, null_rate: 1 }");
        Assert.That(new SemanticValueGenerator(nullField, 1).Next(), Is.Null);
    }

    [Test]
    public void FailsExplicitlyWhenLengthCannotBeSatisfied()
    {
        var field = ParseField("{ generator: finance.card_number, max_length: 1 }");
        Assert.Throws<InvalidOperationException>(() => new SemanticValueGenerator(field, 42).Next());
    }

    private FieldDefinition ParseField(string declaration)
    {
        var result = parser.Parse(Document(declaration));
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        return result.Specification!.Tables["items"].Fields["value"];
    }

    private static string Document(string field) => "spec_version: '1.0'\ntables: { items: { fields: { value: " + field + " } } }";
}
