namespace TestingDataFactory.Core.Specifications.Model;

public enum GenerationMode { Valid, EdgeCases, Anomalies }
public enum SqlDialect { PostgreSql, MySql, SQLite }
public enum SqlOutputStyle { Script, Insert }

public abstract record GenerationOutput;
public sealed record CsvOutput(char Delimiter = ',') : GenerationOutput;
public sealed record JsonOutput : GenerationOutput;
public sealed record SqlOutput(SqlDialect Dialect, SqlOutputStyle? Style = null) : GenerationOutput;

/// <summary>Параметры генерации; неуказанные seed и output остаются неопределёнными.</summary>
public sealed record GenerationOptions(int? Seed, IReadOnlyDictionary<string, int> Rows, GenerationMode Mode, GenerationOutput? Output);

/// <summary>Параметры интерфейса или CLI, заменяющие значения спецификации.</summary>
public sealed record GenerationOverrides(int? Seed = null, IReadOnlyDictionary<string, int>? Rows = null,
    GenerationMode? Mode = null, GenerationOutput? Output = null);
