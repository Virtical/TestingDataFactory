using TestingDataFactory.Contracts;

namespace TestingDataFactory.Worker.Messaging;

internal static class GenerateSpecificationCommandValidator
{
    private const int SupportedContractVersion = 1;

    public static void ValidateContract(GenerateSpecification command, Guid? messageId)
    {
        if (command.JobId == Guid.Empty || messageId != command.JobId)
            throw new InvalidOperationException("GenerateSpecification requires MessageId equal to a non-empty JobId.");

        if (command.ContractVersion != SupportedContractVersion)
            throw new InvalidOperationException("Unsupported GenerateSpecification contract version.");
    }

    public static bool HasValidPayload(GenerateSpecification command) =>
        !string.IsNullOrWhiteSpace(command.Prompt) && command.CreatedAt != default;
}
