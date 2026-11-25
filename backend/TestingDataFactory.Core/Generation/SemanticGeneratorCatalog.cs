using System.Diagnostics.CodeAnalysis;
using Bogus;

namespace TestingDataFactory.Core.Generation;

/// <summary>Реестр генераторов и поддерживаемых локалей библиотеки Bogus.</summary>
internal static class SemanticGeneratorCatalog
{
    private static readonly Dictionary<string, Func<Faker, string>> Generators = new(StringComparer.Ordinal)
    {
        { "person.full_name", static faker => faker.Name.FullName() },
        { "person.first_name", static faker => faker.Name.FirstName() },
        { "person.last_name", static faker => faker.Name.LastName() },
        { "internet.email", static faker => faker.Internet.Email() },
        { "internet.username", static faker => faker.Internet.UserName() },
        { "phone", static faker => faker.Phone.PhoneNumber() },
        { "address.city", static faker => faker.Address.City() },
        { "finance.card_number", static faker => faker.Finance.CreditCardNumber() }
    };

    private static readonly HashSet<string> Locales = new(Database.GetAllLocales(), StringComparer.Ordinal);

    public static bool Contains(string name) => Generators.ContainsKey(name);

    public static bool TryGetGenerator(string name, [NotNullWhen(true)] out Func<Faker, string>? generator) =>
        Generators.TryGetValue(name, out generator);

    /// <summary>Преобразует локаль спецификации в имя локали Bogus без неявной подмены страны.</summary>
    public static bool TryResolveLocale(string locale, out string bogusLocale)
    {
        bogusLocale = locale == "ru_RU" ? "ru" : locale;
        return Locales.Contains(bogusLocale);
    }
}
