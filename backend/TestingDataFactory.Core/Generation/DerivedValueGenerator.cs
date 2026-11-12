using System.Globalization;
using System.Text;
using Cel;
using Google.Api.Expr.V1Alpha1;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Generation;

internal sealed class DerivedValueGenerator
{
    private readonly Expr expression;
    private readonly FieldDefinition field;
    private readonly IReadOnlyDictionary<string, FieldDefinition> fields;
    private readonly HashSet<object>? used;
    private readonly record struct Number(decimal Value, bool Integer);

    internal DerivedValueGenerator(FieldDefinition field, IReadOnlyDictionary<string, FieldDefinition> fields)
    {
        this.field = field;
        this.fields = fields;
        expression = Parse(field.Derived!.Source);
        if (field.Unique) used = new HashSet<object>();
    }

    internal static bool Supports(string source)
    {
        try { return Supported(Parse(source)); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return false; }
    }

    private static Expr Parse(string source)
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var parsed = Cel.Env.NewEnv(EnvOptions.ClearMacros()).Parse(source);
            return parsed.Ast?.Expr ?? throw new InvalidOperationException("Не удалось разобрать derived.");
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }

    private static bool Supported(Expr node) => node.ExprKindCase switch
    {
        Expr.ExprKindOneofCase.IdentExpr or Expr.ExprKindOneofCase.ConstExpr => true,
        Expr.ExprKindOneofCase.ListExpr => node.ListExpr.Elements.All(Supported),
        Expr.ExprKindOneofCase.CallExpr =>
            node.CallExpr.Function is "_+_" or "_-_" or "-_" or "_*_" or "_/_" or "_%_" or "_==_" or "_!=_" or "_<_" or "_<=_" or "_>_" or "_>=_" or "_&&_" or "_||_" or "!_" or "_?_:_" or "@in" or "size" or "contains" or "startsWith" or "endsWith" &&
            (node.CallExpr.Target is null || Supported(node.CallExpr.Target)) && node.CallExpr.Args.All(Supported),
        _ => false
    };

    internal object? Next(IReadOnlyDictionary<string, object?> row)
    {
        var result = Eval(expression, row);
        object? value = result is Number number ? number.Value : result;
        if (value is decimal numeric)
        {
            if (field.Type == FieldType.Integer && decimal.Truncate(numeric) != numeric) throw new InvalidOperationException("Результат derived не является целым.");
            if (field.Type == FieldType.Decimal) numeric = decimal.Round(numeric, field.Scale ?? 2, MidpointRounding.ToEven);
            if (numeric < field.Min || numeric > field.Max) throw new InvalidOperationException("Результат derived за пределами диапазона.");
            value = numeric;
        }
        if (value is null && field.NullRate is not > 0) throw new InvalidOperationException("derived вернул NULL для поля без null_rate.");
        if (value is string text && (text.Length < field.MinLength || text.Length > field.MaxLength)) throw new InvalidOperationException("Результат derived не соответствует ограничениям длины.");
        if (value is not null && used is not null && !used.Add(value)) throw new InvalidOperationException("Результат derived нарушает unique.");
        return value;
    }

    private object? Eval(Expr node, IReadOnlyDictionary<string, object?> row)
    {
        switch (node.ExprKindCase)
        {
            case Expr.ExprKindOneofCase.IdentExpr:
                var name = node.IdentExpr.Name;
                var value = row[name];
                return value is decimal numeric ? new Number(numeric, fields[name].Type == FieldType.Integer) :
                    value is Guid guid ? guid.ToString("D") : value;
            case Expr.ExprKindOneofCase.ConstExpr:
                var c = node.ConstExpr;
                if (c.HasNullValue) return null;
                if (c.HasBoolValue) return c.BoolValue;
                if (c.HasInt64Value) return new Number(c.Int64Value, true);
                if (c.HasDoubleValue) return new Number((decimal)c.DoubleValue, false);
                if (c.HasStringValue) return c.StringValue;
                throw new InvalidOperationException("Неподдерживаемая константа CEL.");
            case Expr.ExprKindOneofCase.ListExpr:
                return node.ListExpr.Elements.Select(x => Eval(x, row)).ToArray();
            case Expr.ExprKindOneofCase.CallExpr:
                return Call(node.CallExpr, row);
            default: throw new InvalidOperationException("Неподдерживаемая конструкция derived.");
        }
    }

    private object? Call(Expr.Types.Call call, IReadOnlyDictionary<string, object?> row)
    {
        var f = call.Function;
        if (f == "_&&_") return (bool)Eval(call.Args[0], row)! && (bool)Eval(call.Args[1], row)!;
        if (f == "_||_") return (bool)Eval(call.Args[0], row)! || (bool)Eval(call.Args[1], row)!;
        if (f == "_?_:_") return Eval(call.Args[(bool)Eval(call.Args[0], row)! ? 1 : 2], row);
        var args = (call.Target is null ? Enumerable.Empty<Expr>() : new[] { call.Target }).Concat(call.Args).Select(x => Eval(x, row)).ToArray();
        var a = args[0]; var b = args.Length > 1 ? args[1] : null;
        if (f == "!_") return !(bool)a!;
        if (f == "-_") { var n = (Number)a!; return new Number(-n.Value, n.Integer); }
        if (f == "@in") return ((object?[])b!).Any(v => Equal(a, v));
        if (f == "_==_") return Equal(a, b);
        if (f == "_!=_") return !Equal(a, b);
        if (f is "_<_" or "_<=_" or "_>_" or "_>=_")
        {
            var c = Compare(a!, b!);
            return f switch { "_<_" => c < 0, "_<=_" => c <= 0, "_>_" => c > 0, _ => c >= 0 };
        }
        if (f == "size") return new Number(((string)a!).EnumerateRunes().Count(), true);
        if (f == "contains") return ((string)a!).Contains((string)b!, StringComparison.Ordinal);
        if (f == "startsWith") return ((string)a!).StartsWith((string)b!, StringComparison.Ordinal);
        if (f == "endsWith") return ((string)a!).EndsWith((string)b!, StringComparison.Ordinal);
        if (f == "_+_" && a is string s) return s + (string)b!;
        var x = (Number)a!; var y = (Number)b!;
        return f switch
        {
            "_+_" => new Number(x.Value + y.Value, x.Integer && y.Integer),
            "_-_" => new Number(x.Value - y.Value, x.Integer && y.Integer),
            "_*_" => new Number(x.Value * y.Value, x.Integer && y.Integer),
            "_/_" => new Number(x.Integer && y.Integer ? decimal.Truncate(x.Value / y.Value) : x.Value / y.Value, x.Integer && y.Integer),
            "_%_" => new Number(x.Value % y.Value, true),
            _ => throw new InvalidOperationException("Неподдерживаемая операция derived: " + f)
        };
    }

    private static bool Equal(object? a, object? b) => a is Number x && b is Number y ? x.Value == y.Value : Equals(a, b);
    private static int Compare(object a, object b) => (a, b) switch
    {
        (Number x, Number y) => x.Value.CompareTo(y.Value),
        (string x, string y) => string.CompareOrdinal(x, y),
        (TemporalBound x, TemporalBound y) => Ticks(x).CompareTo(Ticks(y)),
        _ => throw new InvalidOperationException("Несравнимые значения derived.")
    };
    private static long Ticks(TemporalBound value) => (value.Date?.ToDateTime(TimeOnly.MinValue).Ticks ?? 0) + (value.Time?.Ticks ?? 0) - (value.UtcOffset?.Ticks ?? 0);
}
