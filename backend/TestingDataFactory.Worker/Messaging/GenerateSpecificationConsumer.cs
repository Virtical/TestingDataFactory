using MassTransit;
using TestingDataFactory.Contracts;
using TestingDataFactory.Worker.Specifications;
using TestingDataFactory.Worker.Specifications.Model;

namespace TestingDataFactory.Worker.Messaging;

internal sealed class GenerateSpecificationConsumer(
    IBus bus,
    ISpecificationGenerationService generationService) : IConsumer<GenerateSpecification>
{
    public async Task Consume(ConsumeContext<GenerateSpecification> context)
    {
        var command = context.Message;
        GenerateSpecificationCommandValidator.ValidateContract(command, context.MessageId);

        if (!GenerateSpecificationCommandValidator.HasValidPayload(command))
        {
            await PublishFailureAsync(
                context,
                SpecificationGenerationErrorCodes.InvalidGenerationRequest,
                "Запрос на генерацию не заполнен.");
            return;
        }

        var retryAttempt = context.GetRetryAttempt();
        if (retryAttempt == 0)
        {
            await bus.Publish(new JobStarted(command.JobId, command.ContractVersion, DateTimeOffset.UtcNow),
                publish => publish.CorrelationId = command.JobId, context.CancellationToken);
        }

        SpecificationGenerationResult result;
        try
        {
            result = await generationService.GenerateAsync(command.Prompt, context.CancellationToken);
        }
        catch (TransientModelException) when (retryAttempt >= SpecificationGenerationRetry.RetryLimit
            && !context.CancellationToken.IsCancellationRequested)
        {
            await PublishFailureAsync(context, SpecificationGenerationErrorCodes.ModelTemporarilyUnavailable,
                "Модель временно недоступна. Попробуйте запустить генерацию позже.");
            return;
        }
        switch (result)
        {
            case SpecificationGenerationResult.Succeeded success:
                await context.Publish(
                    new SpecificationGenerated(command.JobId, command.ContractVersion, DateTimeOffset.UtcNow, success.Yaml),
                    publish => publish.CorrelationId = command.JobId, context.CancellationToken);
                break;
            case SpecificationGenerationResult.Failed failure:
                await PublishFailureAsync(context, failure.Code, failure.Message);
                break;
            default:
                throw new InvalidOperationException("Unsupported specification generation result.");
        }
    }

    private static Task PublishFailureAsync(ConsumeContext<GenerateSpecification> context, string code, string message) =>
        context.Publish(new JobFailed(context.Message.JobId, context.Message.ContractVersion, DateTimeOffset.UtcNow, code, message),
            publish => publish.CorrelationId = context.Message.JobId, context.CancellationToken);
}
