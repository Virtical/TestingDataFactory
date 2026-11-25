namespace TestingDataFactory.Worker.Specifications;

internal abstract record SpecificationGenerationResult
{
    private SpecificationGenerationResult() { }

    public sealed record Succeeded(string Yaml) : SpecificationGenerationResult;

    public sealed record Failed(string Code, string Message) : SpecificationGenerationResult;
}
