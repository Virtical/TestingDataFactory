using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Specifications.Parsing;

/// <summary>Результат проверки только верхнего уровня, а не всей спецификации.</summary>
public sealed record SpecificationStructureResult(
    SpecificationStructure? Structure,
    IReadOnlyList<SpecificationDiagnostic> Diagnostics)
{
    public bool IsSuccess => Structure is not null && Diagnostics.Count == 0;
}
