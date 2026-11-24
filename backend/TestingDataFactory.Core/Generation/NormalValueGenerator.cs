using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Generation;

internal sealed class NormalValueGenerator
{
    private const int MaxAttempts = 10_000;
    private readonly Random random;
    private readonly FieldDefinition field;
    private readonly decimal min, max;
    private readonly HashSet<decimal>? used;

    internal NormalValueGenerator(FieldDefinition field, Random random)
    {
        this.field = field;
        this.random = random;
        (min, max) = TableGenerator.NumericBounds(field);
        if (field.Unique) used = new HashSet<decimal>();
    }

    internal decimal Next()
    {
        var normal = (NormalDistribution)field.Distribution!;
        var scale = field.Type == FieldType.Integer ? 0 : field.Scale ?? 2;
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var z = Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());
            var sample = (double)normal.Mean + (double)normal.StandardDeviation * z;
            if (!double.IsFinite(sample) || sample < (double)decimal.MinValue || sample > (double)decimal.MaxValue) continue;
            decimal value;
            try { value = decimal.Round((decimal)sample, scale, MidpointRounding.ToEven); }
            catch (OverflowException) { continue; }
            if (value < min || value > max || (used is not null && !used.Add(value))) continue;
            return value;
        }
        throw new InvalidOperationException($"Не удалось подобрать значение normal за {MaxAttempts} попыток с учётом ограничений и уникальности.");
    }
}
