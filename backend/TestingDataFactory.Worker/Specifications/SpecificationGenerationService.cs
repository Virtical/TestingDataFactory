using TestingDataFactory.Contracts;
using TestingDataFactory.Worker.Specifications.Extraction;
using TestingDataFactory.Worker.Specifications.Model;
using TestingDataFactory.Worker.Specifications.Validation;

namespace TestingDataFactory.Worker.Specifications;

internal sealed class SpecificationGenerationService(
    ISpecificationModelClient modelClient,
    ISpecificationResponseExtractor responseExtractor,
    ISpecificationValidator validator) : ISpecificationGenerationService
{
    public async Task<SpecificationGenerationResult> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        string response;
        try
        {
            response = await modelClient.GenerateAsync(prompt, cancellationToken);
        }
        catch (PermanentModelException exception)
        {
            switch (exception.Failure)
            {
                case PermanentModelFailure.NotConfigured:
                    return new SpecificationGenerationResult.Failed(
                        SpecificationGenerationErrorCodes.ModelNotConfigured,
                        "Генерация спецификаций пока недоступна.");
                case PermanentModelFailure.Rejected:
                    return new SpecificationGenerationResult.Failed(
                        SpecificationGenerationErrorCodes.ModelRejected,
                        "Модель не смогла обработать запрос.");
                default:
                    throw;
            }
        }

        var yaml = responseExtractor.ExtractYaml(response);
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return new SpecificationGenerationResult.Failed(
                SpecificationGenerationErrorCodes.EmptyModelResponse,
                "Модель вернула пустую спецификацию.");
        }

        // TODO: обсудить ограниченный цикл исправления YAML, когда валидатор начнёт возвращать детали ошибок.
        if (!await validator.IsValidAsync(yaml, cancellationToken))
        {
            return new SpecificationGenerationResult.Failed(
                SpecificationGenerationErrorCodes.InvalidSpecification,
                "Модель вернула некорректную спецификацию.");
        }

        return new SpecificationGenerationResult.Succeeded(yaml);
    }
}
