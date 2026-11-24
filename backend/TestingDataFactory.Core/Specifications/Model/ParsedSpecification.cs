using TestingDataFactory.Core.Specifications.Parsing;

namespace TestingDataFactory.Core.Specifications.Model;

/// <summary>Результат разбора, ещё не проверенный на совместимость связей и ограничений.</summary>
public sealed class ParsedSpecification
{
    internal PreparedSpecification Prepared { get; }
    public string Version => Prepared.Structure.Version;
    public string Locale => Prepared.Structure.Locale;
    public GenerationOptions? Generation { get; }

    internal ParsedSpecification(PreparedSpecification prepared, GenerationOptions? generation)
    {
        Prepared = prepared;
        Generation = generation;
    }
}
