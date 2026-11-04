namespace TestingDataFactory.Worker.Specifications.Model;

internal sealed class UnconfiguredSpecificationModelClient : ISpecificationModelClient
{
    public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<string>(new PermanentModelException(PermanentModelFailure.NotConfigured));
    }
}
