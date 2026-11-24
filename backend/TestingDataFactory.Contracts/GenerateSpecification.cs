namespace TestingDataFactory.Contracts;

/// <summary>Запрос генерации YAML-спецификации. MessageId в envelope должен равняться JobId.</summary>
public sealed record GenerateSpecification(
    Guid JobId,
    int ContractVersion,
    DateTimeOffset CreatedAt,
    string Prompt);
