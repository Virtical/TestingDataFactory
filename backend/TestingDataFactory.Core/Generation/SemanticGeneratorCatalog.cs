using Bogus;

namespace TestingDataFactory.Core.Generation;

/// <summary>Поддерживаемые имена генераторов и локали библиотеки Bogus.</summary>
public static class SemanticGeneratorCatalog
{
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        "person.full_name", "person.first_name", "person.last_name", "internet.email", "internet.username",
        "phone", "address.city", "finance.card_number"
    };

    private static readonly HashSet<string> Locales = new(Database.GetAllLocales(), StringComparer.Ordinal);

    public static bool Contains(string name) => Names.Contains(name);

    /// <summary>Преобразует локаль спецификации в имя локали Bogus без неявной подмены страны.</summary>
    public static bool TryResolveLocale(string locale, out string bogusLocale)
    {
        bogusLocale = locale == "ru_RU" ? "ru" : locale;
        return Locales.Contains(bogusLocale);
    }
}
