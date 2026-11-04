using MassTransit;
using TestingDataFactory.Contracts;
using TestingDataFactory.Worker.Specifications.Extraction;
using TestingDataFactory.Worker.Specifications.Model;
using TestingDataFactory.Worker.Specifications.Validation;

namespace TestingDataFactory.Worker.Messaging;

internal sealed class GenerateSpecificationConsumer(
    IBus bus,
    ISpecificationModelClient modelClient,
    ISpecificationResponseExtractor responseExtractor,
    ISpecificationValidator validator) : IConsumer<GenerateSpecification>
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

        await bus.Publish(new JobStarted(command.JobId, command.ContractVersion, DateTimeOffset.UtcNow),
            publish => publish.CorrelationId = command.JobId, context.CancellationToken);

        string response;
        try
        {
            response = await modelClient.GenerateAsync(command.Prompt, context.CancellationToken);
        }
        catch (PermanentModelException exception)
        {
            switch (exception.Failure)
            {
                case PermanentModelFailure.NotConfigured:
                    await PublishFailureAsync(context, SpecificationGenerationErrorCodes.ModelNotConfigured,
                        "Генерация спецификаций пока недоступна.");
                    return;
                case PermanentModelFailure.Rejected:
                    await PublishFailureAsync(context, SpecificationGenerationErrorCodes.ModelRejected,
                        "Модель не смогла обработать запрос.");
                    return;
                default:
                    throw;
            }
        }

        var yaml = responseExtractor.ExtractYaml(response);
        // TODO(этап 5): определить, следует ли повторять генерацию при пустом ответе модели.
        if (string.IsNullOrWhiteSpace(yaml))
        {
            await PublishFailureAsync(context, SpecificationGenerationErrorCodes.EmptyModelResponse,
                "Модель вернула пустую спецификацию.");
            return;
        }

        // TODO: обсудить ограниченный цикл исправления YAML, когда валидатор начнёт возвращать детали ошибок.
        if (!await validator.IsValidAsync(yaml, context.CancellationToken))
        {
            await PublishFailureAsync(context, SpecificationGenerationErrorCodes.InvalidSpecification,
                "Модель вернула некорректную спецификацию.");
            return;
        }

        await context.Publish(new SpecificationGenerated(command.JobId, command.ContractVersion, DateTimeOffset.UtcNow, yaml),
            publish => publish.CorrelationId = command.JobId, context.CancellationToken);
    }

    private static Task PublishFailureAsync(ConsumeContext<GenerateSpecification> context, string code, string message) =>
        context.Publish(new JobFailed(context.Message.JobId, context.Message.ContractVersion, DateTimeOffset.UtcNow, code, message),
            publish => publish.CorrelationId = context.Message.JobId, context.CancellationToken);
}
