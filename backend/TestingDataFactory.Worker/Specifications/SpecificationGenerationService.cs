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
        SpecificationCorrection? correction = null;
        try
        {
            for (var attempt = 0; attempt <= modelOptions.Value.MaxCorrectionAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await RequestModelAsync(prompt, correction, cancellationToken);
                var yaml = responseExtractor.ExtractYaml(response);
                if (string.IsNullOrWhiteSpace(yaml))
                {
                    return new SpecificationGenerationResult.Failed(
                        SpecificationGenerationErrorCodes.EmptyModelResponse,
                        "Модель вернула пустую спецификацию.");
                }

                var diagnostics = await validator.ValidateAsync(yaml, cancellationToken);
                if (diagnostics.Count == 0)
                    return new SpecificationGenerationResult.Succeeded(yaml);

                correction = new SpecificationCorrection(yaml, diagnostics);
            }

            return new SpecificationGenerationResult.Failed(
                SpecificationGenerationErrorCodes.InvalidSpecification,
                "Не удалось получить корректную спецификацию за отведённое число исправлений.");
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

    }

    private async Task<string> RequestModelAsync(string prompt, SpecificationCorrection? correction, CancellationToken cancellationToken)
    {
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestCancellation.CancelAfter(modelOptions.Value.RequestTimeout);

        try
        {
            var response = await modelClient.GenerateAsync(prompt, requestCancellation.Token, correction);
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
