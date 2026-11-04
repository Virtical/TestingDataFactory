using Microsoft.Extensions.Options;
using TestingDataFactory.Contracts;
using TestingDataFactory.Worker.Configuration;
using TestingDataFactory.Worker.Specifications.Extraction;
using TestingDataFactory.Worker.Specifications.Model;
using TestingDataFactory.Worker.Specifications.Validation;

namespace TestingDataFactory.Worker.Specifications;

internal sealed class SpecificationGenerationService(
    ISpecificationModelClient modelClient,
    ISpecificationResponseExtractor responseExtractor,
    ISpecificationValidator validator,
    IOptions<ModelClientOptions> modelOptions) : ISpecificationGenerationService
{
    public async Task<SpecificationGenerationResult> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        string response;
        try
        {
            response = await RequestModelAsync(prompt, cancellationToken);
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

    private async Task<string> RequestModelAsync(string prompt, CancellationToken cancellationToken)
    {
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestCancellation.CancelAfter(modelOptions.Value.RequestTimeout);

        try
        {
            var response = await modelClient.GenerateAsync(prompt, requestCancellation.Token);
            requestCancellation.Token.ThrowIfCancellationRequested();
            return response;
        }
        catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested)
        {
            throw new TransientModelException();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }
}
