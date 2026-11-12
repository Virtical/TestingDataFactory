using Bogus;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Generation;

/// <summary>Генератор значений одного поля с локальным состоянием и seed.</summary>
internal sealed class SemanticValueGenerator
{
    private const int MaxAttempts = 1000;
    private readonly Faker faker;
    private readonly FieldDefinition field;
    private readonly HashSet<string> used = new(StringComparer.Ordinal);

    public SemanticValueGenerator(FieldDefinition field, int seed)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (field.Generator is null || !SemanticGeneratorCatalog.Contains(field.Generator))
            throw new ArgumentException("Поле должно содержать поддерживаемый генератор.", nameof(field));
        if (field.Locale is null || !SemanticGeneratorCatalog.TryResolveLocale(field.Locale, out var locale))
            throw new ArgumentException("Поле должно содержать поддерживаемую локаль.", nameof(field));
        this.field = field;
        faker = new Faker(locale) { Random = new Randomizer(seed) };
    }

    /// <summary>Создаёт значение, учитывая null_rate, длину и уникальность в рамках этого экземпляра.</summary>
    public string? Next()
    {
        if (field.NullRate is > 0 && (decimal)faker.Random.Double() < field.NullRate) return null;
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var value = field.Generator switch
            {
                "person.full_name" => faker.Name.FullName(),
                "person.first_name" => faker.Name.FirstName(),
                "person.last_name" => faker.Name.LastName(),
                "internet.email" => faker.Internet.Email(),
                "internet.username" => faker.Internet.UserName(),
                "phone" => faker.Phone.PhoneNumber(),
                "address.city" => faker.Address.City(),
                "finance.card_number" => faker.Finance.CreditCardNumber(),
                _ => throw new InvalidOperationException("Неизвестный генератор.")
            };
            if (field.MinLength is int min && value.Length < min) continue;
            if (field.MaxLength is int max && value.Length > max) continue;
            if (field.Unique && !used.Add(value)) continue;
            return value;
        }
        throw new InvalidOperationException("Не удалось сгенерировать значение, удовлетворяющее ограничениям поля.");
    }
}
