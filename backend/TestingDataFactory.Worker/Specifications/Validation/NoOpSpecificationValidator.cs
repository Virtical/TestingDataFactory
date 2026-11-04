namespace TestingDataFactory.Worker.Specifications.Validation;

internal sealed class NoOpSpecificationValidator : ISpecificationValidator
{
    public Task<bool> IsValidAsync(string yaml, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }
}
