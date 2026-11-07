namespace TestingDataFactory.Core.Specifications.Model;

/// <summary>Разобранная спецификация с таблицами, доступными по имени.</summary>
public sealed record Specification(string Version, string Locale, IReadOnlyDictionary<string, TypeDefinition> Types, IReadOnlyDictionary<string, TableDefinition> Tables, IReadOnlyList<string> TableOrder, GenerationOptions? Generation = null);

public sealed record TableDefinition(string Name, IReadOnlyDictionary<string, FieldDefinition> Fields, IReadOnlyList<RuleDefinition> Rules);

public enum FieldType
{
    Integer, Decimal, String, Regex, Boolean, Date, DateTime, Time, Uuid, Enum, Reference, Dynamic
}

/// <summary>Поле базового типа. Числовые и временные границы включительные.</summary>
public sealed record FieldDefinition(
    FieldType Type,
    bool Primary,
    bool Unique,
    bool Pii,
    decimal? NullRate,
    decimal? Min,
    decimal? Max,
    int? Scale,
    int? MinLength,
    int? MaxLength,
    string? Pattern,
    decimal? TrueRate,
    string? NamedType = null,
    string? Column = null,
    FieldReference? Reference = null,
    TemporalBound? TemporalMin = null,
    TemporalBound? TemporalMax = null,
    string? Generator = null,
    string? Locale = null,
    CelExpression? Derived = null,
    DistributionDefinition? Distribution = null);

/// <summary>Дата, время или дата и время; отсутствие смещения обозначает локальное время без часового пояса.</summary>
public sealed record TemporalBound(DateOnly? Date, TimeOnly? Time, TimeSpan? UtcOffset);

public sealed record FieldReference(string Table, string Field, PerParentRange? PerParent);

public sealed record PerParentRange(int Min, int Max);
