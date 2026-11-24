using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Specifications.Parsing;

public sealed record SpecificationParseResult(Specification? Specification, IReadOnlyList<SpecificationDiagnostic> Diagnostics)
{
    public bool IsSuccess => Specification is not null && Diagnostics.Count == 0;
}
