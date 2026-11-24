using TestingDataFactory.Core.Specifications.Diagnostics;

namespace TestingDataFactory.Core.Specifications.Model;

public sealed record GenerationOptionsResult(GenerationOptions? Options, IReadOnlyList<SpecificationDiagnostic> Diagnostics)
{
    public bool IsSuccess => Options is not null && Diagnostics.Count == 0;
}
