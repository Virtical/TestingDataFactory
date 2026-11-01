namespace TestingDataFactory.Contracts;

/// <summary>YAML-спецификация успешно сгенерирована моделью</summary>
public sealed record SpecificationGenerated(
    Guid JobId,
    int ContractVersion,
    DateTimeOffset GeneratedAt,
    string SpecificationYaml);
