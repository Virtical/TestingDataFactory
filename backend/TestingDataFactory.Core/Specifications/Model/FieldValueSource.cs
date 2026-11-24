namespace TestingDataFactory.Core.Specifications.Model;

/// <summary>Способ получения значения поля независимо от типа результата.</summary>
public abstract record FieldValueSource
{
    private protected FieldValueSource() { }

    public sealed record ByType : FieldValueSource;

    public sealed record Generator(string Name) : FieldValueSource
    {
        public string Name { get; } = RequireName(Name, nameof(Name));
    }

    public sealed record Derived(CelExpression Expression) : FieldValueSource
    {
        public CelExpression Expression { get; } = Expression ?? throw new ArgumentNullException(nameof(Expression));
    }

    internal static string RequireName(string name, string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, parameter);
        return name;
    }
}

public sealed record FieldReference(string Table, string Field, PerParentRange? PerParent) : FieldValueSource
{
    public string Table { get; } = RequireName(Table, nameof(Table));
    public string Field { get; } = RequireName(Field, nameof(Field));
}
