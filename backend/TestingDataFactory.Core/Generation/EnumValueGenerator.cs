using System.Numerics;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Generation;

internal sealed class EnumValueGenerator
{
    private readonly IReadOnlyList<EnumEntry> values;
    private readonly BigInteger[] weights;
    private readonly BigInteger[] tree;
    private readonly Random random;
    private readonly bool unique;
    private BigInteger total;

    internal EnumValueGenerator(IReadOnlyList<EnumEntry> values, bool unique, Random random)
    {
        this.values = values;
        this.unique = unique;
        this.random = random;
        // Use exact scaled integers, including for extreme decimal weights.
        var maxScale = values.Max(v => decimal.GetBits(v.Weight ?? 1m)[3] >> 16 & 0xff);
        weights = new BigInteger[values.Count];
        tree = new BigInteger[values.Count + 1];
        for (var i = 0; i < values.Count; i++)
        {
            var bits = decimal.GetBits(values[i].Weight ?? 1m);
            var scale = (bits[3] >> 16) & 0xff;
            var magnitude = (new BigInteger((uint)bits[2]) << 64) | (new BigInteger((uint)bits[1]) << 32) | (uint)bits[0];
            weights[i] = magnitude * BigInteger.Pow(10, maxScale - scale);
            total += weights[i];
            Update(i, weights[i]);
        }
    }

    internal string Next()
    {
        if (total <= 0) throw new InvalidOperationException("Исчерпаны уникальные значения перечисления.");
        var draw = TableGenerator.NextBelow(random, total);
        var index = 0;
        var mask = 1;
        while (mask <= values.Count / 2) mask <<= 1;
        for (; mask != 0; mask >>= 1)
        {
            var next = index + mask;
            if (next < tree.Length && tree[next] <= draw)
            {
                draw -= tree[next];
                index = next;
            }
        }
        if (unique)
        {
            var weight = weights[index];
            total -= weight;
            Update(index, -weight);
            weights[index] = 0;
        }
        return values[index].Value;
    }

    private void Update(int index, BigInteger amount)
    {
        for (var i = index + 1; i < tree.Length; i += i & -i) tree[i] += amount;
    }
}
