namespace TestingDataFactory.Core.Specifications.Parsing;

/// <summary>Метаданные справочников для проверки спецификации, без загрузки их содержимого.</summary>
public interface ISpecificationDictionaryRegistry
{
    /// <summary>Возвращает имена столбцов многоколоночного справочника; пустой список означает простой справочник.</summary>
    bool TryGetColumns(string name, out IReadOnlyList<string> columns);
}
