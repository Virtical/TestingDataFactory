namespace TestingDataFactory.Core.Specifications.Model;

/// <summary>Параметры распределения при генерации числового поля.</summary>
public abstract record DistributionDefinition;

public sealed record UniformDistribution : DistributionDefinition;

public sealed record LogNormalDistribution : DistributionDefinition;

public sealed record NormalDistribution(decimal Mean, decimal StandardDeviation) : DistributionDefinition;
