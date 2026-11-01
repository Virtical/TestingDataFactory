using MassTransit;
using TestingDataFactory.Contracts;
using TestingDataFactory.Specifications;

namespace TestingDataFactory.Messaging;

internal sealed class SpecificationJobEventsConsumer(SpecificationJobEventHandler handler) :
    IConsumer<JobStarted>, IConsumer<SpecificationGenerated>, IConsumer<JobFailed>
{
    public Task Consume(ConsumeContext<JobStarted> context)
    {
        ValidateContract(context.Message.JobId, context.Message.ContractVersion);
        return handler.ApplyAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<SpecificationGenerated> context)
    {
        ValidateContract(context.Message.JobId, context.Message.ContractVersion);
        if (context.Message.SpecificationYaml is null)
            throw new InvalidOperationException("SpecificationYaml is required.");
        return handler.ApplyAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<JobFailed> context)
    {
        ValidateContract(context.Message.JobId, context.Message.ContractVersion);
        if (string.IsNullOrWhiteSpace(context.Message.ErrorCode) || string.IsNullOrWhiteSpace(context.Message.ErrorMessage))
            throw new InvalidOperationException("ErrorCode and ErrorMessage are required.");
        return handler.ApplyAsync(context.Message, context.CancellationToken);
    }

    private static void ValidateContract(Guid jobId, int contractVersion)
    {
        if (jobId == Guid.Empty) throw new InvalidOperationException("JobId is required.");
        if (contractVersion != 1) throw new InvalidOperationException("Unsupported job event contract version.");
    }
}
