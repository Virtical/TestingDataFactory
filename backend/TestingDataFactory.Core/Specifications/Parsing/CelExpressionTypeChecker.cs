using Google.Api.Expr.V1Alpha1;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Specifications.Parsing;

// CEL variables remain dynamic in Cel.NET: the documented format permits mixed integer/decimal
// arithmetic and nullable values. Check the known schema separately without rejecting those cases.
internal static class CelExpressionTypeChecker
{
    private enum Kind { Unknown, Null, Boolean, Integer, Decimal, String, Date, DateTime, Time, Object, Array, List }
    private readonly record struct Value(Kind Kind, FieldDefinition? Field = null);

    public static string? Validate(Expr expr, IReadOnlyDictionary<string, FieldType>? types,
        IReadOnlyDictionary<string, FieldDefinition>? fields, bool booleanResult, FieldType? expectedType)
    {
        var (value, error) = Infer(expr, types, fields);
        if (error is not null) return error;
        if (booleanResult && !Is(value.Kind, Kind.Boolean)) return "Выражение правила должно возвращать логическое значение.";
        if (expectedType is not null && !Assignable(ToKind(expectedType.Value), value.Kind))
            return "Тип результата выражения несовместим с типом поля.";
        return null;
    }

    private static (Value Value, string? Error) Infer(Expr expr, IReadOnlyDictionary<string, FieldType>? types,
        IReadOnlyDictionary<string, FieldDefinition>? fields)
    {
        switch (expr.ExprKindCase)
        {
            case Expr.ExprKindOneofCase.IdentExpr:
                var name = expr.IdentExpr.Name;
                if (fields is not null && fields.TryGetValue(name, out var field)) return (new(ToKind(field.Type), field), null);
                return (new(types is not null && types.TryGetValue(name, out var type) ? ToKind(type) : Kind.Unknown), null);
            case Expr.ExprKindOneofCase.SelectExpr:
                var (parent, parentError) = Infer(expr.SelectExpr.Operand, types, fields);
                if (parentError is not null) return (default, parentError);
                if (parent.Kind != Kind.Object || parent.Field?.Fields is null)
                    return (default, "Обращение к вложенному полю допустимо только для объекта с известной структурой.");
                if (!parent.Field.Fields.TryGetValue(expr.SelectExpr.Field, out var child))
                    return (default, "Неизвестное вложенное поле: " + expr.SelectExpr.Field);
                return (new(ToKind(child.Type), child), null);
            case Expr.ExprKindOneofCase.ConstExpr:
                var c = expr.ConstExpr;
                return (new(c.HasNullValue ? Kind.Null : c.HasBoolValue ? Kind.Boolean : c.HasInt64Value ? Kind.Integer :
                    c.HasDoubleValue ? Kind.Decimal : c.HasStringValue ? Kind.String : Kind.Unknown), null);
            case Expr.ExprKindOneofCase.ListExpr:
                return (new(Kind.List), null);
            case Expr.ExprKindOneofCase.CallExpr:
                return InferCall(expr.CallExpr, types, fields);
            default:
                return (new(Kind.Unknown), null);
        }
    }

    private static (Value Value, string? Error) InferCall(Expr.Types.Call call,
        IReadOnlyDictionary<string, FieldType>? types, IReadOnlyDictionary<string, FieldDefinition>? fields)
    {
        var args = new List<Value>();
        if (call.Target is not null)
        {
            var (target, error) = Infer(call.Target, types, fields);
            if (error is not null) return (default, error);
            args.Add(target);
        }
        foreach (var arg in call.Args)
        {
            var (value, error) = Infer(arg, types, fields);
            if (error is not null) return (default, error);
            args.Add(value);
        }
        var a = args.Count > 0 ? args[0].Kind : Kind.Unknown;
        var b = args.Count > 1 ? args[1].Kind : Kind.Unknown;
        var function = call.Function;
        var valid = true;
        var result = Kind.Unknown;
        switch (function)
        {
            case "_&&_": case "_||_":
                valid = Is(a, Kind.Boolean) && Is(b, Kind.Boolean); result = Kind.Boolean; break;
            case "!_":
                valid = Is(a, Kind.Boolean); result = Kind.Boolean; break;
            case "_?_:_":
                var other = args.Count > 2 ? args[2].Kind : Kind.Unknown;
                valid = Is(a, Kind.Boolean) && Compatible(b, other);
                result = Merge(b, other); break;
            case "_==_": case "_!=_":
                valid = Compatible(a, b); result = Kind.Boolean; break;
            case "_<_": case "_<=_": case "_>_": case "_>=_":
                valid = Comparable(a, b); result = Kind.Boolean; break;
            case "_+_":
                if (Is(a, Kind.String) && Is(b, Kind.String) && (a == Kind.String || b == Kind.String))
                    result = Kind.String;
                else { valid = Numeric(a) && Numeric(b); result = MergeNumeric(a, b); }
                break;
            case "_-_": case "_*_": case "_/_":
                valid = Numeric(a) && Numeric(b); result = MergeNumeric(a, b); break;
            case "-_":
                valid = Numeric(a); result = a; break;
            case "_%_":
                valid = Is(a, Kind.Integer) && Is(b, Kind.Integer); result = Kind.Integer; break;
            case "@in":
                if (call.Args.Count == 2 && call.Args[1].ExprKindCase == Expr.ExprKindOneofCase.ListExpr)
                    foreach (var item in call.Args[1].ListExpr.Elements)
                    {
                        var (itemValue, error) = Infer(item, types, fields);
                        if (error is not null) return (default, error);
                        valid &= Compatible(a, itemValue.Kind);
                    }
                result = Kind.Boolean; break;
            case "size":
                valid = Is(a, Kind.String) || a == Kind.Array; result = Kind.Integer; break;
            case "contains": case "startsWith": case "endsWith": case "matches":
                valid = Is(a, Kind.String) && Is(b, Kind.String); result = Kind.Boolean; break;
        }
        return valid ? (new(result), null) : (default, "Несовместимые типы аргументов операции или функции: " + function);
    }

    private static Kind ToKind(FieldType type) => type switch
    {
        FieldType.Integer => Kind.Integer, FieldType.Decimal => Kind.Decimal, FieldType.Boolean => Kind.Boolean,
        FieldType.String or FieldType.Regex or FieldType.Uuid or FieldType.Enum => Kind.String,
        FieldType.Date => Kind.Date, FieldType.DateTime => Kind.DateTime, FieldType.Time => Kind.Time,
        FieldType.Object => Kind.Object, FieldType.Array => Kind.Array, _ => Kind.Unknown
    };
    private static bool Is(Kind actual, Kind expected) => actual == Kind.Unknown || actual == expected;
    private static bool Numeric(Kind kind) => kind is Kind.Unknown or Kind.Integer or Kind.Decimal;
    private static bool Compatible(Kind a, Kind b) => a == Kind.Unknown || b == Kind.Unknown || a == Kind.Null || b == Kind.Null || a == b || (Numeric(a) && Numeric(b));
    private static bool Comparable(Kind a, Kind b) => (a == Kind.Unknown && b != Kind.Null && b != Kind.Boolean && b != Kind.Object && b != Kind.Array) ||
        (b == Kind.Unknown && a != Kind.Null && a != Kind.Boolean && a != Kind.Object && a != Kind.Array) ||
        (Numeric(a) && Numeric(b)) || (a == b && a is Kind.String or Kind.Date or Kind.DateTime or Kind.Time);
    private static Kind Merge(Kind a, Kind b) => a == Kind.Null ? b : b == Kind.Null ? a : a == b ? a : Numeric(a) && Numeric(b) ? MergeNumeric(a, b) : Kind.Unknown;
    private static Kind MergeNumeric(Kind a, Kind b) => a == Kind.Decimal || b == Kind.Decimal ? Kind.Decimal : a == Kind.Integer && b == Kind.Integer ? Kind.Integer : Kind.Unknown;
    private static bool Assignable(Kind expected, Kind actual) => expected == Kind.Unknown || actual is Kind.Unknown or Kind.Null || expected == actual || (expected == Kind.Decimal && actual == Kind.Integer);
}
