namespace TestingDataFactory.Contracts;

/// <summary>Ошибка генерации спецификации</summary>
public sealed record JobFailed(
    Guid JobId,
    int ContractVersion,
    DateTimeOffset FailedAt,
    string ErrorCode,
    string ErrorMessage);
