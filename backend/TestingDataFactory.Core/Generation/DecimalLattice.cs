using System.Globalization;
using System.Numerics;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Generation;

internal static class DecimalLattice
{
    internal static (BigInteger Min, BigInteger Max, int Scale) Bounds(FieldDefinition field)
    {
        var (min, max) = TableGenerator.NumericBounds(field);
        var scale = field.Scale ?? 2;
        return (Scaled(min, scale, true), Scaled(max, scale, false), scale);
    }

    private static BigInteger Scaled(decimal value, int scale, bool ceiling)
    {
        var bits = decimal.GetBits(value);
        var integer = (new BigInteger((uint)bits[2]) << 64) | (new BigInteger((uint)bits[1]) << 32) | (uint)bits[0];
        if ((bits[3] & int.MinValue) != 0) integer = -integer;
        var sourceScale = (bits[3] >> 16) & 0xff;
        if (sourceScale <= scale) return integer * BigInteger.Pow(10, scale - sourceScale);
        var quotient = BigInteger.DivRem(integer, BigInteger.Pow(10, sourceScale - scale), out var remainder);
        if (remainder != 0 && ceiling && integer > 0) quotient++;
        if (remainder != 0 && !ceiling && integer < 0) quotient--;
        return quotient;
    }

    internal static decimal ToDecimal(BigInteger value, int scale)
    {
        var sign = value.Sign < 0 ? "-" : "";
        var digits = BigInteger.Abs(value).ToString(CultureInfo.InvariantCulture);
        if (scale == 0) return decimal.Parse(sign + digits, CultureInfo.InvariantCulture);
        digits = digits.PadLeft(scale + 1, '0');
        return decimal.Parse(sign + digits[..^scale] + "." + digits[^scale..], CultureInfo.InvariantCulture);
    }
}
