namespace TestingDataFactory.Core.Specifications.Model;

/// <summary>Объявленный пользователем тип.</summary>
public abstract record TypeDefinition
{
    public string? Locale { get; init; }
}

/// <summary>Базовый тип с ограничениями по умолчанию.</summary>
public sealed record PrimitiveTypeDefinition(FieldDefinition Field) : TypeDefinition;

public abstract record EnumTypeDefinition : TypeDefinition;

public sealed record EnumEntry(string Value, decimal? Weight);

public sealed record ValuesEnumDefinition(IReadOnlyList<EnumEntry> Values) : EnumTypeDefinition;

public sealed record TableEnumDefinition(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows) : EnumTypeDefinition;

public sealed record DictionaryEnumDefinition(string Dictionary, IReadOnlyList<string> Columns, CelExpression? Where = null) : EnumTypeDefinition;
