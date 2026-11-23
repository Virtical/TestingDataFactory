using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Generation;

internal static class BuiltInEnumValues
{
    // A small manually curated starting set, not a complete registry of Russian cities.
    internal static readonly IReadOnlyList<EnumEntry> RussianCities = Array.AsReadOnly(new[]
    {
        "Москва", "Санкт-Петербург", "Новосибирск", "Екатеринбург", "Казань", "Нижний Новгород",
        "Красноярск", "Челябинск", "Самара", "Уфа", "Ростов-на-Дону", "Омск", "Краснодар",
        "Воронеж", "Пермь", "Волгоград", "Саратов", "Тюмень", "Тольятти", "Ижевск",
        "Барнаул", "Ульяновск", "Иркутск", "Хабаровск", "Владивосток"
    }.Select(city => new EnumEntry(city, null)).ToArray());

    internal static IReadOnlyList<EnumEntry>? Resolve(Specification specification, FieldDefinition field)
    {
        if (field.NamedType is null || !specification.Types.TryGetValue(field.NamedType, out var type)) return null;
        return type switch
        {
            ValuesEnumDefinition values => values.Values,
            DictionaryEnumDefinition { Dictionary: "russian_cities", Where: null } => RussianCities,
            _ => null
        };
    }
}
