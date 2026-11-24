using System.Diagnostics;
using MassTransit;
using TestingDataFactory.Contracts;
using TestingDataFactory.Worker.Specifications;
using TestingDataFactory.Worker.Specifications.Model;

namespace TestingDataFactory.Worker.Messaging;

internal sealed class GenerateSpecificationConsumer(
    IBus bus,
    ISpecificationGenerationService generationService,
    ILogger<GenerateSpecificationConsumer> logger) : IConsumer<GenerateSpecification>
{
    public async Task Consume(ConsumeContext<GenerateSpecification> context)
    {
        var command = context.Message;
        GenerateSpecificationCommandValidator.ValidateContract(command, context.MessageId);

        var startedAt = Stopwatch.GetTimestamp();
        var retryAttempt = context.GetRetryAttempt();
        logger.LogInformation("Handling {EventType}: {JobId}, attempt {Attempt}",
            nameof(GenerateSpecification), command.JobId, retryAttempt + 1);

        if (!GenerateSpecificationCommandValidator.HasValidPayload(command))
        {
            await PublishFailureAsync(
                context,
                SpecificationGenerationErrorCodes.InvalidGenerationRequest,
                "Запрос на генерацию не заполнен.",
                startedAt);
            return;
        }

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
                "Модель временно недоступна. Попробуйте запустить генерацию позже.", startedAt);
            return;
        }
        catch (TransientModelException) when (!context.CancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("{EventType} will retry: {JobId}, attempt {Attempt}, {ElapsedMilliseconds} ms, {ErrorCode}",
                nameof(GenerateSpecification), command.JobId, retryAttempt + 1,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                SpecificationGenerationErrorCodes.ModelTemporarilyUnavailable);
            throw;
        }

        switch (result)
        {
            case SpecificationGenerationResult.Succeeded success:
                await context.Publish(
                    new SpecificationGenerated(command.JobId, command.ContractVersion, DateTimeOffset.UtcNow, success.Yaml),
                    publish => publish.CorrelationId = command.JobId, context.CancellationToken);
                logger.LogInformation("Buffered {EventType} in outbox: {JobId}, attempt {Attempt}, {ElapsedMilliseconds} ms",
                    nameof(SpecificationGenerated), command.JobId, retryAttempt + 1,
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                break;
            case SpecificationGenerationResult.Failed failure:
                await PublishFailureAsync(context, failure.Code, failure.Message, startedAt);
                break;
            default:
                throw new InvalidOperationException("Unsupported specification generation result.");
        }
    }

    private async Task PublishFailureAsync(
        ConsumeContext<GenerateSpecification> context, string code, string message, long startedAt)
    {
        await context.Publish(new JobFailed(context.Message.JobId, context.Message.ContractVersion, DateTimeOffset.UtcNow, code, message),
            publish => publish.CorrelationId = context.Message.JobId, context.CancellationToken);
        logger.LogWarning("Buffered {EventType} in outbox: {JobId}, attempt {Attempt}, {ElapsedMilliseconds} ms, {ErrorCode}",
            nameof(JobFailed), context.Message.JobId, context.GetRetryAttempt() + 1,
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, code);
    }
}
