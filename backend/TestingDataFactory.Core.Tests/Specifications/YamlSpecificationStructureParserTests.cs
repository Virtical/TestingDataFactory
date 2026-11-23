using NUnit.Framework;
using TestingDataFactory.Core.Specifications.Parsing;

namespace TestingDataFactory.Core.Tests.Specifications;

public class YamlSpecificationStructureParserTests
{
    private readonly YamlSpecificationStructureParser parser = new();

    [Test]
    public void ReadsSectionsWithoutClaimingToValidateTheirContents()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            types: { money: { type: decimal } }
            tables: { users: { fields: { id: { type: future_type } } } }
            generation: { mode: future_mode }
            """);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Structure!.Version, Is.EqualTo("1.0"));
        Assert.That(result.Structure.Locale, Is.EqualTo("ru_RU"));
        Assert.That(result.Structure.Sections, Is.EquivalentTo(new[] { "spec_version", "types", "tables", "generation" }));
        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void ReadsExplicitLocale()
    {
        var result = parser.Parse("spec_version: \"1.0\"\nlocale: en_US\ntables: {}\n");
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Structure!.Locale, Is.EqualTo("en_US"));
    }

    [TestCase("tables: {}", "MISSING_VERSION")]
    [TestCase("spec_version: '1.0'", "MISSING_TABLES")]
    [TestCase("spec_version: 1.0\ntables: {}", "INVALID_VERSION")]
    [TestCase("spec_version: '2.0'\ntables: {}", "UNSUPPORTED_VERSION")]
    [TestCase("spec_version: '1.0'\ntables: []", "SECTION_NOT_MAPPING")]
    [TestCase("spec_version: '1.0'\ntables: {}\ntypes: null", "SECTION_NOT_MAPPING")]
    [TestCase("spec_version: '1.0'\ntables: {}\ngeneration: []", "SECTION_NOT_MAPPING")]
    [TestCase("spec_version: '1.0'\ntables: {}\nlocale: null", "INVALID_LOCALE")]
    [TestCase("spec_version: '1.0'\ntables: {}\nlocale: 123", "INVALID_LOCALE")]
    [TestCase("spec_version: '1.0'\ntables: {}\nextra: {}", "UNKNOWN_SECTION")]
    [TestCase("spec_version: '1.0'\ntables: {}\ntables: {}", "DUPLICATE_SECTION")]
    [TestCase("- a\n- b", "ROOT_NOT_MAPPING")]
    [TestCase("", "DOCUMENT_COUNT")]
    [TestCase("spec_version: '1.0'\ntables: {", "INVALID_YAML")]
    [TestCase("spec_version: '1.0'\ntables: {}\n---\ntables: {}", "DOCUMENT_COUNT")]
    [TestCase("spec_version: '1.0'\ntypes: &shared {}\ntables: *shared", "ALIAS_NOT_SUPPORTED")]
    [TestCase("spec_version: !!int '1.0'\ntables: {}", "TAG_NOT_SUPPORTED")]
    [TestCase("spec_version: '1.0'\ntables: {}\nlocale: !!int '123'", "TAG_NOT_SUPPORTED")]
    public void ReportsInvalidStructure(string yaml, string code)
    {
        var result = parser.Parse(yaml);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Structure, Is.Null);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(code));
    }

    [Test]
    public void ReportsPathAndPositionForUnknownSection()
    {
        var result = parser.Parse("spec_version: '1.0'\ntables: {}\ntypo: {}\n");
        var error = result.Diagnostics.Single();
        Assert.That(error.Path, Is.EqualTo("$.'typo'"));
        Assert.That(error.Line, Is.EqualTo(3));
        Assert.That(error.Column, Is.EqualTo(1));
    }

    [Test]
    public void CollectsIndependentErrors()
    {
        var result = parser.Parse("locale: []\nother: 1\n");
        Assert.That(result.Diagnostics.Select(d => d.Code), Is.EquivalentTo(new[]
        {
            "INVALID_LOCALE", "UNKNOWN_SECTION", "MISSING_VERSION", "MISSING_TABLES"
        }));
    }

    [Test]
    public void RejectsExcessiveDepth()
    {
        var yaml = "spec_version: '1.0'\ntables: " + new string('[', 65) + "0" + new string(']', 65);
        Assert.That(parser.Parse(yaml).Diagnostics.Select(d => d.Code), Does.Contain("MAX_DEPTH_EXCEEDED"));
    }

    [Test]
    public void RejectsExcessiveLength()
    {
        Assert.That(parser.Parse(new string(' ', 1_000_001)).Diagnostics.Select(d => d.Code), Does.Contain("DOCUMENT_TOO_LARGE"));
    }
}
