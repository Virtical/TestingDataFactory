using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Specifications;

public sealed record ValidationResult(Specification? Specification, GenerationOptions? Options, IReadOnlyList<SpecificationDiagnostic> Diagnostics)
{
    public bool IsSuccess => Specification is not null && Diagnostics.Count == 0;
}
