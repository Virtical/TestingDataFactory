using NUnit.Framework;
using TestingDataFactory.Core.Specifications.Model;
using TestingDataFactory.Core.Specifications.Parsing;

namespace TestingDataFactory.Core.Tests.Specifications;

public class YamlGenerationOptionsTests
{
    private readonly YamlSpecificationParser parser = new();
    private const string Tables = "tables: { customers: { fields: { id: { type: integer } } }, orders: { fields: { customer_id: { ref: customers.id, per_parent: { min: 0, max: 8 } } } } }";

    [Test]
    public void ParsesGenerationAndExplicitSqlOptions()
    {
        var yaml = "spec_version: '1.0'\n" + Tables + "\ngeneration: { seed: 42, rows: { customers: 500 }, mode: edge_cases, output: { sql: { dialect: postgresql, style: script } } }";
        var result = parser.Parse(yaml);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        var options = result.Specification!.Generation!;
        Assert.That(options.Seed, Is.EqualTo(42));
        Assert.That(options.Rows["customers"], Is.EqualTo(500));
        Assert.That(options.Mode, Is.EqualTo(GenerationMode.EdgeCases));
        Assert.That(options.Output, Is.EqualTo(new SqlOutput(SqlDialect.PostgreSql, SqlOutputStyle.Script)));
    }

    [Test]
    public void ParsesDocumentWithGeneratorsRelationsDerivedAndOutput()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            locale: ru_RU
            types:
              product: { enum: [Ноутбук, Мышь] }
            tables:
              customers:
                fields:
                  id: { type: integer, primary: true }
                  name: { generator: person.full_name }
              orders:
                fields:
                  customer_id: { ref: customers.id, per_parent: { min: 0, max: 8 } }
                  product: { type: product }
                  quantity: { type: integer, min: 1, max: 5 }
                  price: { type: decimal, min: 100, scale: 2, distribution: lognormal }
                  amount: { type: decimal, scale: 2, derived: quantity * price }
            generation:
              seed: 2026
              rows: { customers: 500 }
              output: { sql: { dialect: postgresql, style: script } }
            """);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.That(result.Specification!.Generation!.Rows["customers"], Is.EqualTo(500));
        Assert.That(result.Specification.Tables["orders"].Fields["amount"].Derived, Is.Not.Null);
    }

    [Test]
    public void UsesSpecifiedDefaultsWithoutInventingSeedOrOutput()
    {
        var options = ParseGeneration("{}");
        Assert.That(options.Mode, Is.EqualTo(GenerationMode.Valid));
        Assert.That(options.Seed, Is.Null);
        Assert.That(options.Output, Is.Null);
        Assert.That(options.Rows, Is.Empty);
        Assert.That(ParseGeneration("{ output: { csv: {} } }").Output, Is.EqualTo(new CsvOutput(',')));
        Assert.That(ParseGeneration("{ output: { sql: { dialect: mysql } } }").Output, Is.EqualTo(new SqlOutput(SqlDialect.MySql)));
        Assert.That(parser.Parse("spec_version: '1.0'\n" + Tables).Specification!.Generation, Is.Null);
    }

    [TestCase("{ output: { json: {} }, mode: anomalies }", typeof(JsonOutput))]
    [TestCase("{ output: { csv: { delimiter: ';' } } }", typeof(CsvOutput))]
    [TestCase("{ output: { sql: { dialect: sqlite, style: insert } } }", typeof(SqlOutput))]
    public void SupportsDeclaredOutputFormats(string body, Type type)
    {
        Assert.That(ParseGeneration(body).Output, Is.TypeOf(type));
    }

    [TestCase("{ seed: 1.5 }", "INVALID_SEED")]
    [TestCase("{ seed: '10' }", "INVALID_SEED")]
    [TestCase("{ rows: [] }", "INVALID_ROWS")]
    [TestCase("{ rows: { customers: -1 } }", "INVALID_ROW_COUNT")]
    [TestCase("{ rows: { customers: 2.5 } }", "INVALID_ROW_COUNT")]
    [TestCase("{ rows: { missing: 1 } }", "UNKNOWN_ROWS_TABLE")]
    [TestCase("{ rows: { orders: 1 } }", "ROWS_FOR_CHILD_TABLE")]
    [TestCase("{ mode: anything }", "INVALID_GENERATION_MODE")]
    [TestCase("{ output: {} }", "INVALID_OUTPUT")]
    [TestCase("{ output: { csv: {}, json: {} } }", "INVALID_OUTPUT")]
    [TestCase("{ output: { xml: {} } }", "INVALID_OUTPUT")]
    [TestCase("{ output: { csv: [] } }", "INVALID_OUTPUT")]
    [TestCase("{ output: { csv: { delimiter: 'ab' } } }", "INVALID_CSV_DELIMITER")]
    [TestCase("{ output: { json: { delimiter: ';' } } }", "UNSUPPORTED_PARAMETER")]
    [TestCase("{ output: { sql: {} } }", "MISSING_SQL_DIALECT")]
    [TestCase("{ output: { sql: { dialect: oracle } } }", "INVALID_SQL_DIALECT")]
    [TestCase("{ output: { sql: { dialect: sqlite, style: fancy } } }", "INVALID_SQL_STYLE")]
    [TestCase("{ output: { sql: { dialect: sqlite, extra: 1 } } }", "UNSUPPORTED_PARAMETER")]
    [TestCase("{ unknown: 1 }", "UNSUPPORTED_PARAMETER")]
    public void ReportsInvalidGenerationOptions(string body, string code)
    {
        var result = parser.Parse("spec_version: '1.0'\n" + Tables + "\ngeneration: " + body);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(code));
    }

    [Test]
    public void RowsErrorContainsSourcePosition()
    {
        var result = parser.Parse("spec_version: '1.0'\n" + Tables + "\ngeneration:\n  rows:\n    orders: 4\n");
        var error = result.Diagnostics.Single(d => d.Code == "ROWS_FOR_CHILD_TABLE");
        Assert.That(error.Path, Is.EqualTo("$.generation.rows['orders']"));
        Assert.That(error.Line, Is.EqualTo(5));
    }

    [TestCase("[]")]
    [TestCase("bad")]
    [TestCase("{ mode: [bad], rows: { x: -1 }, output: { strange: {} } }")]
    [TestCase("{ x: 1, x: 2 }")]
    [TestCase("{ x: !unknown value }")]
    [TestCase("{ x: *missing }")]
    [TestCase("!unknown value")]
    [TestCase("*missing")]
    [TestCase("")]
    public void DataValidationIgnoresGenerationContents(string generation)
    {
        var result = parser.Parse("spec_version: '1.0'\ngeneration: " + generation + "\n" + Tables, SpecificationParsePurpose.DataValidation);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.That(result.Specification!.Generation, Is.Null);
    }

    [Test]
    public void DataValidationIgnoresBlockAndPreservesPositions()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            generation:
              mode: invalid
              rows: { x: -1, x: 7 }
            tables:
              t:
                fields:
                  id: { type: absent }
            """, SpecificationParsePurpose.DataValidation);
        var diagnostic = result.Diagnostics.Single(d => d.Code == "INVALID_FIELD_TYPE");
        Assert.That(diagnostic.Line, Is.EqualTo(8));
        Assert.That(diagnostic.Path, Is.EqualTo("$.tables['t'].fields['id'].type"));
    }

    [Test]
    public void DataValidationIgnoresFlowBlockWithoutCorruptingOtherKeys()
    {
        var result = parser.Parse("{ spec_version: '1.0', generation: { x: 1, x: 2 }, tables: { t: { fields: { id: { type: integer } } } } }", SpecificationParsePurpose.DataValidation);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
    }

    [Test]
    public void DataValidationHandlesUnicodeBeforeAndInsideIgnoredBlock()
    {
        var result = parser.Parse("""
            spec_version: '1.0'
            # 😀 Проверка позиций
            generation:
              message: '😀'
              rows: { x: 1, x: 2 }
            tables:
              t:
                fields:
                  id: { type: integer }
            """, SpecificationParsePurpose.DataValidation);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
    }

    [Test]
    public void DataValidationDoesNotIgnoreOtherUnsupportedYamlFeatures()
    {
        var result = parser.Parse("spec_version: '1.0'\ngeneration: { x: *missing }\ntables: &tables {}\ntypes: *tables", SpecificationParsePurpose.DataValidation);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("ALIAS_NOT_SUPPORTED"));
    }

    [Test]
    public void DataValidationStillRejectsMalformedYaml()
    {
        var result = parser.Parse("spec_version: '1.0'\ngeneration: [unterminated\n" + Tables, SpecificationParsePurpose.DataValidation);
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INVALID_YAML"));
    }

    [Test]
    public void OverridesReplaceSpecifiedValuesAndMergeRowsWithoutMutation()
    {
        var specification = parser.Parse("spec_version: '1.0'\n" + Tables + "\ngeneration: { seed: 1, rows: { customers: 10 }, output: { csv: {} } }").Specification!;
        var overrides = new GenerationOverrides(Seed: 2, Rows: new Dictionary<string, int> { ["customers"] = 30 }, Mode: GenerationMode.Anomalies, Output: new JsonOutput());
        var result = GenerationOptionsMerger.Apply(specification, overrides);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Options!.Seed, Is.EqualTo(2));
        Assert.That(result.Options.Rows["customers"], Is.EqualTo(30));
        Assert.That(result.Options.Mode, Is.EqualTo(GenerationMode.Anomalies));
        Assert.That(result.Options.Output, Is.TypeOf<JsonOutput>());
        Assert.That(specification.Generation!.Seed, Is.EqualTo(1));
        Assert.That(specification.Generation.Rows["customers"], Is.EqualTo(10));
    }

    [Test]
    public void OverridesAreRevalidated()
    {
        var specification = parser.Parse("spec_version: '1.0'\n" + Tables).Specification!;
        var result = GenerationOptionsMerger.Apply(specification, new GenerationOverrides(Rows: new Dictionary<string, int> { ["orders"] = 2, ["missing"] = -1 }, Output: new CsvOutput('\n')));
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("ROWS_FOR_CHILD_TABLE"));
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("UNKNOWN_ROWS_TABLE"));
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INVALID_ROW_COUNT"));
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("INVALID_CSV_DELIMITER"));
    }

    private GenerationOptions ParseGeneration(string value)
    {
        var result = parser.Parse("spec_version: '1.0'\n" + Tables + "\ngeneration: " + value);
        Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        return result.Specification!.Generation!;
    }
}
