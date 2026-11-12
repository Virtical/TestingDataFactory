using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Generation;

internal sealed class LogNormalValueGenerator
{
    private readonly FieldDefinition field;
    private readonly Random random;
    private readonly HashSet<decimal>? used;
    private readonly double mu, sigma;
    private readonly decimal min, max;

    internal LogNormalValueGenerator(FieldDefinition field, Random random)
    {
        this.field = field;
        this.random = random;
        (min, max) = TableGenerator.NumericBounds(field);
        var scale = field.Type == FieldType.Integer ? 0 : field.Scale ?? 2;
        var quantum = Math.Pow(10, -scale);
        var lower = Math.Max((double)min, quantum);
        var upper = (double)max;
        mu = (Math.Log(lower) + Math.Log(upper)) / 2;
        sigma = (Math.Log(upper) - Math.Log(lower)) / 6;
        if (field.Unique) used = new HashSet<decimal>();
    }

    internal decimal Next()
    {
        var scale = field.Type == FieldType.Integer ? 0 : field.Scale ?? 2;
        for (var attempt = 0; attempt < 10_000; attempt++)
        {
            var z = Math.Sqrt(-2 * Math.Log(1 - random.NextDouble())) * Math.Cos(2 * Math.PI * random.NextDouble());
            var sample = Math.Exp(mu + sigma * z);
            if (!double.IsFinite(sample) || sample > (double)decimal.MaxValue) continue;
            decimal value;
            try { value = decimal.Round((decimal)sample, scale, MidpointRounding.ToEven); }
            catch (OverflowException) { continue; }
            if (value <= 0 || value < min || value > max || (used is not null && !used.Add(value))) continue;
            return value;
        }
        throw new InvalidOperationException("Не удалось подобрать значение lognormal за 10000 попыток.");
    }
}
