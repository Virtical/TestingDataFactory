using TestingDataFactory.Core.Specifications.Diagnostics;

namespace TestingDataFactory.Worker.Specifications.Model;

public sealed record SpecificationCorrection(string Yaml, IReadOnlyList<SpecificationDiagnostic> Diagnostics);
