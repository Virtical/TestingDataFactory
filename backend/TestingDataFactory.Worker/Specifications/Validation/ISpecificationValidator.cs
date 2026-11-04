namespace TestingDataFactory.Worker.Specifications.Validation;

public interface ISpecificationValidator
{
    Task<bool> IsValidAsync(string yaml, CancellationToken cancellationToken);
}
