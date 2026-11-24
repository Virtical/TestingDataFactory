using TestingDataFactory.Core.Specifications;
using TestingDataFactory.Core.Specifications.Diagnostics;

namespace TestingDataFactory.Worker.Specifications.Validation;

internal sealed class CoreSpecificationValidator : ISpecificationValidator
{
    public Task<IReadOnlyList<SpecificationDiagnostic>> ValidateAsync(string yaml, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var parser = new SpecificationParser();
        var parsed = parser.Parse(yaml);
        var diagnostics = parsed.IsSuccess
            ? parser.ValidateGenerationOptions(parsed.Specification!).Diagnostics
            : parsed.Diagnostics;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(diagnostics);
    }
}
