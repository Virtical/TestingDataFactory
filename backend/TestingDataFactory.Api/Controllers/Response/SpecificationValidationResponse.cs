using TestingDataFactory.Core.Specifications.Diagnostics;

namespace TestingDataFactory.Api.Controllers.Response;

public sealed record SpecificationValidationResponse(IReadOnlyList<SpecificationDiagnostic> Diagnostics)
{
    public bool IsValid => Diagnostics.Count == 0;
}
