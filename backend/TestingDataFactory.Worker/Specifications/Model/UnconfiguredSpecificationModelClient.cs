namespace TestingDataFactory.Worker.Specifications.Model;

internal sealed class UnconfiguredSpecificationModelClient : ISpecificationModelClient
{
    public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken, SpecificationCorrection? correction = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<string>(new PermanentModelException(PermanentModelFailure.NotConfigured));
    }
}
