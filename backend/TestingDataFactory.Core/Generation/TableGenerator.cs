using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Text.RegularExpressions;
using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Generation;

internal static class TableGenerator
{
    internal static (decimal Min, decimal Max) NumericBounds(FieldDefinition field)
    {
        var min = field.Min ?? (field.Max is { } max ? Math.Min(0m, max) : 0m);
        var upper = field.Max ?? Math.Max(100m, min);
        return (min, upper);
    }

    internal static List<SpecificationDiagnostic> Check(Specification specification, GenerationOptions options)
    {
        var errors = new List<SpecificationDiagnostic>();
        if (options.Mode != GenerationMode.Valid)
            errors.Add(new("UNSUPPORTED_GENERATION_MODE", "Пока поддерживается только режим valid.", "$.generation.mode"));
        if (options.Output is not (null or CsvOutput))
            errors.Add(new("UNSUPPORTED_OUTPUT", "Пока поддерживается только вывод CSV или поток строк.", "$.generation.output"));
        foreach (var (name, table) in specification.Tables)
        {
            var tablePath = "$.tables['" + name.Replace("'", "\\'") + "']";
            var references = table.Fields.Where(p => p.Value.Reference is not null).ToArray();
            if (references.Length > 1)
                errors.Add(new("UNSUPPORTED_MULTIPLE_PARENTS", "Пока поддерживается только одна ссылка в дочерней таблице.", tablePath));
            if (references.Any(p => p.Value.Unique || p.Value.NullRate is > 0))
                errors.Add(new("UNSUPPORTED_REFERENCE_CONSTRAINT", "Для ref пока не поддерживаются unique, primary и null_rate.", tablePath));
            foreach (var (_, referenceField) in references)
            {
                var reference = referenceField.Reference!;
                if (specification.Tables[reference.Table].Fields[reference.Field].NullRate is > 0)
                    errors.Add(new("UNSUPPORTED_NULLABLE_PARENT", "Целевое поле ref не должно допускать NULL.", tablePath));
            }
            if (references.Length == 0 && !options.Rows.TryGetValue(name, out _))
            {
                errors.Add(new("MISSING_ROW_COUNT", "Укажите число строк таблицы.", "$.generation.rows['" + name.Replace("'", "\\'") + "']"));
            }
            var count = options.Rows.GetValueOrDefault(name);
            if (table.Rules.Count != 0)
                errors.Add(new("UNSUPPORTED_GENERATION_RULES", "Генерация с rules пока не поддерживается.", tablePath + ".rules"));
            foreach (var (fieldName, field) in table.Fields)
            {
                var path = tablePath + ".fields['" + fieldName.Replace("'", "\\'") + "']";
                var enumValues = field.Type == FieldType.Enum ? BuiltInEnumValues.Resolve(specification, field) : null;
                if (field.Reference is not null) continue;
                if (field.Derived is not null)
                {
                    if (field.Type is not (FieldType.Integer or FieldType.Decimal or FieldType.Boolean or FieldType.String) ||
                        field.Scale is > 28 || !DerivedValueGenerator.Supports(field.Derived.Source))
                        errors.Add(new("UNSUPPORTED_DERIVED", "Выражение или тип derived пока не поддерживается генератором.", path + ".derived"));
                    continue;
                }
                if ((field.Type == FieldType.Enum && enumValues is null) ||
                    (field.Generator is null && field.Type is not (FieldType.Integer or FieldType.Decimal or FieldType.Boolean or FieldType.Uuid or FieldType.Date or FieldType.DateTime or FieldType.Time or FieldType.Enum)))
                {
                    errors.Add(new("UNSUPPORTED_GENERATION_FIELD", "Тип поля или способ задания значения пока не поддерживается.", path));
                    continue;
                }
                if (field.Distribution is not (null or UniformDistribution or NormalDistribution or LogNormalDistribution))
                    errors.Add(new("UNSUPPORTED_GENERATION_DISTRIBUTION", "Неизвестное распределение.", path + ".distribution"));
                if (field.Type == FieldType.Decimal && field.Scale is > 28)
                    errors.Add(new("UNSUPPORTED_DECIMAL_SCALE", "При генерации decimal scale не может быть больше 28.", path + ".scale"));
                else if (field.Type == FieldType.Decimal)
                {
                    var bounds = DecimalLattice.Bounds(field);
                    if (bounds.Min > bounds.Max || BigInteger.Abs(bounds.Min) > new BigInteger(decimal.MaxValue) || BigInteger.Abs(bounds.Max) > new BigInteger(decimal.MaxValue))
                        errors.Add(new("UNSUPPORTED_DECIMAL_RANGE", "Диапазон нельзя представить с заданным scale.", path));
                }
                if (field.Distribution is LogNormalDistribution && (field.Scale is null or <= 28) && NumericBounds(field).Max < (field.Type == FieldType.Integer ? 1m : DecimalLattice.ToDecimal(BigInteger.One, field.Scale ?? 2)))
                    errors.Add(new("INVALID_LOGNORMAL_RANGE", "Для lognormal требуется положительное значение в диапазоне.", path));
                if (!field.Unique || field.NullRate == 1 || count <= 0) continue;
                BigInteger? capacity = field.Type switch
                {
                    FieldType.Integer => new BigInteger(NumericBounds(field).Max) - new BigInteger(NumericBounds(field).Min) + 1,
                    FieldType.Boolean => field.TrueRate is 0 or 1 ? 1 : 2,
                    FieldType.Date or FieldType.DateTime or FieldType.Time => new BigInteger(TemporalGeneration.Bounds(field).Max) - TemporalGeneration.Bounds(field).Min + 1,
                    FieldType.Enum => enumValues!.Count(value => value.Weight is null or > 0),
                    FieldType.Decimal when field.Scale is null or <= 28 => DecimalLattice.Bounds(field).Max - DecimalLattice.Bounds(field).Min + 1,
                    _ => null
                };
                if (capacity is not null && count > capacity.Value)
                    errors.Add(new(field.NullRate is > 0 ? "UNSUPPORTED_NULLABLE_UNIQUE_CAPACITY" : "INSUFFICIENT_UNIQUE_VALUES",
                        field.NullRate is > 0
                            ? "Число строк превышает число уникальных не-NULL значений; при заданном null_rate генерация не гарантирована."
                            : "Число строк превышает количество возможных уникальных значений.", path));
            }
        }
        return errors;
    }

    internal static IEnumerable<GeneratedRow> Generate(Specification specification, GenerationOptions options)
    {
        var random = new Random(options.Seed!.Value);
        var targets = specification.Tables.Values.SelectMany(t => t.Fields.Values).Where(f => f.Reference is not null)
            .Select(f => (f.Reference!.Table, f.Reference.Field)).Distinct().ToDictionary(x => x, _ => new List<object?>());
        foreach (var tableName in specification.TableOrder)
        {
            var table = specification.Tables[tableName];
            var fields = table.Fields.Where(p => p.Value.Reference is null && p.Value.Derived is null)
                .ToDictionary(p => p.Key, p => new FieldGenerator(p.Value, random, p.Key, specification), StringComparer.Ordinal);
            var derived = OrderDerived(table).Select(name => (Name: name, Generator: new DerivedValueGenerator(table.Fields[name], table.Fields))).ToArray();
            var reference = table.Fields.FirstOrDefault(p => p.Value.Reference is not null);
            IEnumerable<(object? Parent, long Count)> groups = reference.Value is null
                ? new[] { ((object?)null, (long)options.Rows[tableName]) }
                : targets[(reference.Value.Reference!.Table, reference.Value.Reference.Field)].Select(parent =>
                    (parent, random.NextInt64(reference.Value.Reference.PerParent?.Min ?? 1, (long)(reference.Value.Reference.PerParent?.Max ?? 1) + 1)));
            foreach (var (parent, count) in groups)
            for (long row = 0; row < count; row++)
            {
                var values = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var (name, generator) in fields) values.Add(name, generator.Next());
                if (reference.Value is not null)
                {
                    if (parent is decimal n && (n < reference.Value.Min || n > reference.Value.Max))
                        throw new InvalidOperationException($"Значение ref поля '{reference.Key}' не соответствует ограничениям.");
                    if (parent is string s && (s.Length < reference.Value.MinLength || s.Length > reference.Value.MaxLength ||
                        (reference.Value.Pattern is { } pattern && !Regex.IsMatch(s, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))))
                        throw new InvalidOperationException($"Значение ref поля '{reference.Key}' не соответствует ограничениям.");
                    if (parent is TemporalBound temporal && !TemporalReferenceMatches(temporal, reference.Value))
                        throw new InvalidOperationException($"Значение ref поля '{reference.Key}' не соответствует временным ограничениям.");
                    values.Add(reference.Key, parent);
                }
                foreach (var (name, generator) in derived)
                {
                    try { values.Add(name, generator.Next(values)); }
                    catch (Exception ex) when (ex is InvalidOperationException or ArithmeticException or RegexMatchTimeoutException)
                    { throw new InvalidOperationException($"Ошибка derived поля '{name}': {ex.Message}", ex); }
                }
                foreach (var target in targets.Where(p => p.Key.Table == tableName)) target.Value.Add(values[target.Key.Field]);
                var ordered = table.Fields.Keys.ToDictionary(name => name, name => values[name], StringComparer.Ordinal);
                yield return new GeneratedRow(tableName, new ReadOnlyDictionary<string, object?>(ordered));
            }
        }
    }

    private static IEnumerable<string> OrderDerived(TableDefinition table)
    {
        var pending = table.Fields.Where(p => p.Value.Derived is not null).ToDictionary(p => p.Key, p => p.Value.Derived!, StringComparer.Ordinal);
        while (pending.Count != 0)
        {
            var ready = pending.Where(p => p.Value.Dependencies.All(d => !pending.ContainsKey(d))).Select(p => p.Key).ToArray();
            if (ready.Length == 0) throw new InvalidOperationException("Цикл derived.");
            foreach (var name in ready) { pending.Remove(name); yield return name; }
        }
    }

    private static bool TemporalReferenceMatches(TemporalBound value, FieldDefinition field)
    {
        static long Ticks(TemporalBound b) => (b.Date?.ToDateTime(TimeOnly.MinValue).Ticks ?? 0) + (b.Time?.Ticks ?? 0) - (b.UtcOffset?.Ticks ?? 0);
        if (field.TemporalMin is { } min && (min.UtcOffset.HasValue != value.UtcOffset.HasValue || Ticks(value) < Ticks(min))) return false;
        if (field.TemporalMax is { } max && (max.UtcOffset.HasValue != value.UtcOffset.HasValue || Ticks(value) > Ticks(max))) return false;
        return true;
    }

    private sealed class FieldGenerator
    {
        private readonly FieldDefinition field;
        private readonly Random random;
        private readonly string name;
        private readonly BigInteger min, range, start, step;
        private readonly TimeSpan? temporalOffset;
        private readonly SemanticValueGenerator? semantic;
        private readonly NormalValueGenerator? normal;
        private readonly LogNormalValueGenerator? lognormal;
        private readonly EnumValueGenerator? enumGenerator;
        private readonly int decimalScale;
        private BigInteger next;
        private bool usedTrue, usedFalse;
        private readonly byte[] uuidPrefix = new byte[8];
        private ulong uuidCounter;

        internal FieldGenerator(FieldDefinition field, Random random, string name, Specification specification)
        {
            this.field = field;
            this.random = random;
            this.name = name;
            if (field.Generator is not null) semantic = new SemanticValueGenerator(field, random.Next());
            if (field.Type == FieldType.Enum) enumGenerator = new EnumValueGenerator(BuiltInEnumValues.Resolve(specification, field)!, field.Unique, random);
            if (field.Distribution is NormalDistribution) normal = new NormalValueGenerator(field, random);
            if (field.Distribution is LogNormalDistribution) lognormal = new LogNormalValueGenerator(field, random);
            if (field.Type == FieldType.Integer && normal is null && lognormal is null)
            {
                var bounds = NumericBounds(field);
                min = new BigInteger(bounds.Min);
                range = new BigInteger(bounds.Max) - min + 1;
                if (field.Unique)
                {
                    start = NextBelow(random, range);
                    step = ChooseStep(random, range);
                }
            }
            if (field.Type == FieldType.Decimal && normal is null && lognormal is null)
            {
                var bounds = DecimalLattice.Bounds(field);
                min = bounds.Min;
                range = bounds.Max - min + 1;
                decimalScale = bounds.Scale;
                if (field.Unique) { start = NextBelow(random, range); step = ChooseStep(random, range); }
            }
            if (field.Type is FieldType.Date or FieldType.DateTime or FieldType.Time)
            {
                var bounds = TemporalGeneration.Bounds(field);
                min = bounds.Min;
                range = new BigInteger(bounds.Max) - min + 1;
                temporalOffset = bounds.Offset;
                if (field.Unique)
                {
                    start = NextBelow(random, range);
                    step = ChooseStep(random, range);
                }
            }
            if (field.Type == FieldType.Uuid) random.NextBytes(uuidPrefix);
        }

        internal object? Next()
        {
            if (semantic is not null)
            {
                try { return semantic.Next(); }
                catch (InvalidOperationException ex) { throw new InvalidOperationException($"Поле '{name}': {ex.Message}", ex); }
            }
            if (field.NullRate == 1 || (field.NullRate is > 0 && (decimal)random.NextDouble() < field.NullRate)) return null;
            if (normal is not null)
            {
                try { return normal.Next(); }
                catch (InvalidOperationException ex) { throw new InvalidOperationException($"Поле '{name}': {ex.Message}", ex); }
            }
            if (lognormal is not null)
            {
                try { return lognormal.Next(); }
                catch (InvalidOperationException ex) { throw new InvalidOperationException($"Поле '{name}': {ex.Message}", ex); }
            }
            switch (field.Type)
            {
                case FieldType.Enum:
                    return enumGenerator!.Next();
                case FieldType.Integer:
                    if (field.Unique && next >= range) throw new InvalidOperationException($"Исчерпан диапазон уникальных значений поля '{name}'.");
                    var offset = field.Unique ? (start + next++ * step) % range : NextBelow(random, range);
                    return (decimal)(min + offset);
                case FieldType.Decimal:
                    if (field.Unique && next >= range) throw new InvalidOperationException($"Исчерпан диапазон уникальных значений поля '{name}'.");
                    var decimalOffset = field.Unique ? (start + next++ * step) % range : NextBelow(random, range);
                    return DecimalLattice.ToDecimal(min + decimalOffset, decimalScale);
                case FieldType.Boolean:
                    var rate = field.TrueRate ?? 0.5m;
                    var value = (decimal)random.NextDouble() < rate;
                    if (field.Unique)
                    {
                        if (usedTrue && usedFalse) throw new InvalidOperationException($"Исчерпаны уникальные значения поля '{name}'.");
                        if (value && usedTrue) value = false;
                        else if (!value && usedFalse) value = true;
                        if (value) usedTrue = true; else usedFalse = true;
                    }
                    return value;
                case FieldType.Date:
                case FieldType.DateTime:
                case FieldType.Time:
                    if (field.Unique && next >= range) throw new InvalidOperationException($"Исчерпан диапазон уникальных значений поля '{name}'.");
                    var temporal = field.Unique ? (start + next++ * step) % range : NextBelow(random, range);
                    return TemporalGeneration.FromNumber((long)(min + temporal), field.Type, temporalOffset);
                case FieldType.Uuid:
                    var bytes = new byte[16];
                    uuidPrefix.CopyTo(bytes, 0);
                    BinaryPrimitives.WriteUInt64BigEndian(bytes.AsSpan(8), uuidCounter++);
                    bytes[7] = (byte)((bytes[7] & 0x0f) | 0x40);
                    bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
                    return new Guid(bytes);
                default: throw new InvalidOperationException("Неподдерживаемый тип поля.");
            }
        }
    }

    private static BigInteger ChooseStep(Random random, BigInteger range)
    {
        if (range == 1) return BigInteger.Zero;
        for (var i = 0; i < 64; i++)
        {
            var candidate = NextBelow(random, range - 1) + 1;
            if (BigInteger.GreatestCommonDivisor(candidate, range) == 1) return candidate;
        }
        return BigInteger.One;
    }

    internal static BigInteger NextBelow(Random random, BigInteger exclusive)
    {
        if (exclusive <= 1) return BigInteger.Zero;
        var max = exclusive - 1;
        var bytes = max.ToByteArray(isUnsigned: true, isBigEndian: false);
        var highBits = (int)(max.GetBitLength() % 8);
        while (true)
        {
            random.NextBytes(bytes);
            if (highBits != 0) bytes[^1] &= (byte)((1 << highBits) - 1);
            var value = new BigInteger(bytes, isUnsigned: true, isBigEndian: false);
            if (value < exclusive) return value;
        }
    }
}
