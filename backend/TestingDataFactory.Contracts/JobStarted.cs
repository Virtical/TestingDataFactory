namespace TestingDataFactory.Contracts;

/// <summary>Worker приступил к обработке задачи.</summary>
public sealed record JobStarted(
    Guid JobId,
    int ContractVersion,
    DateTimeOffset StartedAt);
