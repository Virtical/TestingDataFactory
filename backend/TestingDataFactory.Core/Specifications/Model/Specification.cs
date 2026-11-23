namespace TestingDataFactory.Core.Specifications.Model;

/// <summary>Разобранная спецификация с таблицами, доступными по имени.</summary>
public sealed record Specification(string Version, string Locale, IReadOnlyDictionary<string, TypeDefinition> Types, IReadOnlyDictionary<string, TableDefinition> Tables, IReadOnlyList<string> TableOrder, GenerationOptions? Generation = null);

public sealed record TableDefinition(string Name, IReadOnlyDictionary<string, FieldDefinition> Fields, IReadOnlyList<RuleDefinition> Rules);

public enum FieldType
{
    Integer, Decimal, String, Regex, Boolean, Date, DateTime, Time, Uuid, Enum, Dynamic = 10, Object, Array
}

/// <summary>Тип результата, источник значения и ограничения поля. Границы включительные.</summary>
public sealed record FieldDefinition(
    FieldType Type,
    FieldValueSource ValueSource,
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
    TemporalBound? TemporalMin = null,
    TemporalBound? TemporalMax = null,
    string? Locale = null,
    DistributionDefinition? Distribution = null,
    IReadOnlyDictionary<string, FieldDefinition>? Fields = null,
    FieldDefinition? Items = null,
    int? MinItems = null,
    int? MaxItems = null)
{
    public FieldValueSource ValueSource
    {
        get;
        init => field = value ?? throw new ArgumentNullException(nameof(ValueSource));
    } = ValueSource ?? throw new ArgumentNullException(nameof(ValueSource));

    public FieldReference? Reference => ValueSource as FieldReference;
    public string? Generator => (ValueSource as FieldValueSource.Generator)?.Name;
    public CelExpression? Derived => (ValueSource as FieldValueSource.Derived)?.Expression;

    internal bool AllowsNull => NullRate is > 0;
}

/// <summary>Дата, время или дата и время; отсутствие смещения обозначает локальное время без часового пояса.</summary>
public sealed record TemporalBound(DateOnly? Date, TimeOnly? Time, TimeSpan? UtcOffset);

public sealed record PerParentRange(int Min, int Max);
