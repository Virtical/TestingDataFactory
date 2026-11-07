namespace TestingDataFactory.Core.Specifications.Parsing;

/// <summary>Справочники, упомянутые в спецификации формата.</summary>
public sealed class DefaultSpecificationDictionaryRegistry : ISpecificationDictionaryRegistry
{
    private static readonly IReadOnlyList<string> WorldCityColumns = Array.AsReadOnly(new[] { "country", "city", "currency" });

    public bool TryGetColumns(string name, out IReadOnlyList<string> columns)
    {
        columns = name == "world_cities" ? WorldCityColumns : Array.Empty<string>();
        return name is "world_cities" or "russian_cities";
    }
}
