using TestingDataFactory.Core.Specifications.Diagnostics;

namespace TestingDataFactory.Worker.Specifications.Validation;

public interface ISpecificationValidator
{
    Task<IReadOnlyList<SpecificationDiagnostic>> ValidateAsync(string yaml, CancellationToken cancellationToken);
}
