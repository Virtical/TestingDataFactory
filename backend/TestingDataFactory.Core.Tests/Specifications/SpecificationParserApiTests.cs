using System.Reflection;
using System.Text.Json;
using NUnit.Framework;
using TestingDataFactory.Core.Specifications;
using TestingDataFactory.Core.Specifications.Parsing;

namespace TestingDataFactory.Core.Tests.Specifications;

public class SpecificationParserApiTests
{
    [Test]
    public void PublicParserExposesParseValidationAndGenerationMethods()
    {
        var methods = typeof(SpecificationParser).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.That(methods.Select(m => m.Name),
            Is.EquivalentTo(new[] { "Parse", "Validate", "ValidateGenerationOptions", "ValidateGeneration", "Generate" }));
        var parse = methods.Single(m => m.Name == "Parse");
        Assert.That(parse.ReturnType, Is.EqualTo(typeof(ParseResult)));
        Assert.That(parse.GetParameters().Select(p => p.ParameterType), Is.EqualTo(new[] { typeof(string) }));
    }

    [Test]
    public void PublicParserReturnsDiagnosticsForInvalidSpecification()
    {
        var result = new SpecificationParser().Parse("spec_version: '2.0'\ntables: {}\n");
        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Specification, Is.Null);
        Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain("UNSUPPORTED_VERSION"));
    }

    [Test]
    public void UnsupportedParameterIncludesPathPositionAndAllowedKeys()
    {
        var result = new SpecificationParser().Parse("""
            spec_version: '1.0'
            tables: { t: { fields: { id: { type: integer } } } }
            generation:
              output:
                sql:
                  dialect: sqlite
                  styl: insert
            """);

        var diagnostic = result.Diagnostics.Single(d => d.Code == "UNSUPPORTED_PARAMETER");
        Assert.That(diagnostic.Path, Is.EqualTo("$.generation.output.sql['styl']"));
        Assert.That(diagnostic.Line, Is.EqualTo(7));
        Assert.That(diagnostic.Column, Is.EqualTo(7));
        Assert.That(diagnostic.AllowedKeys, Is.EqualTo(new[] { "dialect", "style" }));

        var json = JsonSerializer.SerializeToElement(diagnostic, JsonSerializerOptions.Web);
        Assert.That(json.GetProperty("path").GetString(), Is.EqualTo(diagnostic.Path));
        Assert.That(json.GetProperty("allowedKeys").EnumerateArray().Select(key => key.GetString()), Is.EqualTo(diagnostic.AllowedKeys));
    }

    [Test]
    public void UnsupportedParameterIncludesEmptyListWhenNodeAcceptsNoKeys()
    {
        var result = new SpecificationParser().Parse("""
            spec_version: '1.0'
            tables: { t: { fields: { id: { type: integer } } } }
            generation: { output: { json: { delimiter: ',' } } }
            """);

        var diagnostic = result.Diagnostics.Single(d => d.Code == "UNSUPPORTED_PARAMETER");
        Assert.That(diagnostic.AllowedKeys, Is.Empty);
    }
}
