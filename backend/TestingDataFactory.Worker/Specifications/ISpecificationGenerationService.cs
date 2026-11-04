namespace TestingDataFactory.Worker.Specifications;

internal interface ISpecificationGenerationService
{
    Task<SpecificationGenerationResult> GenerateAsync(string prompt, CancellationToken cancellationToken);
}
