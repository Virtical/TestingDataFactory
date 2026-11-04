namespace TestingDataFactory.Worker.Specifications.Model;

public interface ISpecificationModelClient
{
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken);
}
