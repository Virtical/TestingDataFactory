using System.Reflection;
using NUnit.Framework;
using TestingDataFactory.Core.Specifications;
using TestingDataFactory.Core.Specifications.Parsing;

namespace TestingDataFactory.Core.Tests.Specifications;

public class SpecificationParserApiTests
{
    [Test]
    public void PublicParserExposesParseValidateAndGenerate()
    {
        var methods = typeof(SpecificationParser).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.That(methods.Select(m => m.Name), Is.EquivalentTo(new[] { "Parse", "Validate", "Generate" }));
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
}
