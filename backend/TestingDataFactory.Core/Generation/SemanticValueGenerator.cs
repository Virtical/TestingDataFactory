using Bogus;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Generation;

/// <summary>Генератор значений одного поля с локальным состоянием и seed.</summary>
internal sealed class SemanticValueGenerator
{
    private const int MaxAttempts = 1000;
    private readonly Faker faker;
    private readonly Func<Faker, string> generate;
    private readonly FieldDefinition field;
    private readonly HashSet<string> used = new(StringComparer.Ordinal);

    public SemanticValueGenerator(FieldDefinition field, int seed)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (field.ValueSource is not FieldValueSource.Generator generator || !SemanticGeneratorCatalog.TryGetGenerator(generator.Name, out var generate))
            throw new ArgumentException("Поле должно содержать поддерживаемый генератор.", nameof(field));
        if (field.Locale is null || !SemanticGeneratorCatalog.TryResolveLocale(field.Locale, out var locale))
            throw new ArgumentException("Поле должно содержать поддерживаемую локаль.", nameof(field));
        this.field = field;
        this.generate = generate;
        faker = new Faker(locale) { Random = new Randomizer(seed) };
    }

    /// <summary>Создаёт значение, учитывая null_rate, длину и уникальность в рамках этого экземпляра.</summary>
    public string? Next()
    {
        if (field.NullRate is > 0 && (decimal)faker.Random.Double() < field.NullRate) return null;
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var value = generate(faker);
            if (field.MinLength is int min && value.Length < min) continue;
            if (field.MaxLength is int max && value.Length > max) continue;
            if (field.Unique && !used.Add(value)) continue;
            return value;
        }
        throw new InvalidOperationException("Не удалось сгенерировать значение, удовлетворяющее ограничениям поля.");
    }
}
