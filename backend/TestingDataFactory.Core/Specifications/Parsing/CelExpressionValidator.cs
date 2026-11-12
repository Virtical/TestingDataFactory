using System.Globalization;
using Cel;
using Cel.Checker;
using Google.Api.Expr.V1Alpha1;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Specifications.Parsing;

/// <summary>Проверяет CEL и ограничивает его подмножеством формата спецификации.</summary>
internal static class CelExpressionValidator
{
    private static readonly HashSet<string> AllowedCalls = new(StringComparer.Ordinal)
    {
        "_==_", "_!=_", "_<_", "_<=_", "_>_", "_>=_", "_&&_", "_||_", "!_", "_?_:_",
        "_+_", "_-_", "-_", "_*_", "_/_", "_%_", "@in", "size", "contains", "startsWith", "endsWith", "matches"
    };

    // Only defer a syntactically valid expression that actually uses an unavailable field.
    // Syntax and unsupported-operation errors can still be reported independently.
    public static bool ReferencesAny(string source, IReadOnlySet<string> unavailable)
    {
        if (source.Length > 4096 || NestingTooDeep(source)) return false;
        var savedCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var parsed = Cel.Env.NewEnv(EnvOptions.ClearMacros()).Parse(source);
            if (parsed.HasIssues() || parsed.Ast is null) return false;
            var dependencies = new HashSet<string>(StringComparer.Ordinal);
            return CheckNode(parsed.Ast.Expr, dependencies, false, 0, out _) && dependencies.Overlaps(unavailable);
        }
        finally { CultureInfo.CurrentCulture = savedCulture; }
    }

    public static (CelExpression? Expression, string? Error) Validate(string source, IEnumerable<string> variables,
        bool booleanResult = false, FieldType? expectedType = null, IReadOnlyDictionary<string, FieldType>? variableTypes = null,
        IReadOnlyDictionary<string, FieldDefinition>? fields = null)
    {
        if (source.Length > 4096 || NestingTooDeep(source)) return (null, "Выражение слишком длинное или слишком глубоко вложено.");
        var savedCulture = CultureInfo.CurrentCulture;
        try
        {
            // Cel.NET разбирает десятичные литералы в текущей культуре; формат CEL всегда использует точку.
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var declarations = variables.Distinct(StringComparer.Ordinal).Select(name => Decls.NewVar(name, Decls.Dyn)).ToArray();
            var environment = Cel.Env.NewEnv(EnvOptions.ClearMacros(), EnvOptions.Declarations(declarations));
            var parsed = environment.Parse(source);
            if (parsed.HasIssues() || parsed.Ast is null) return (null, parsed.Issues?.ToString() ?? "Не удалось разобрать CEL-выражение.");
            var dependencies = new HashSet<string>(StringComparer.Ordinal);
            if (!CheckNode(parsed.Ast.Expr, dependencies, false, 0, out var error)) return (null, error);
            var checkedAst = environment.Check(parsed.Ast);
            if (checkedAst.HasIssues() || checkedAst.Ast is null) return (null, checkedAst.Issues?.ToString() ?? "Не удалось проверить CEL-выражение.");
            var typeError = CelExpressionTypeChecker.Validate(parsed.Ast.Expr, variableTypes, fields, booleanResult, expectedType);
            if (typeError is not null) return (null, typeError);
            var result = checkedAst.Ast.ResultType;
            if (result is null) return (null, "Не удалось определить тип результата CEL-выражения.");
            if (result.Equals(Decls.Dyn) && parsed.Ast.Expr.ExprKindCase == Expr.ExprKindOneofCase.IdentExpr &&
                variableTypes is not null && variableTypes.TryGetValue(parsed.Ast.Expr.IdentExpr.Name, out var fieldType))
                result = fieldType switch
                {
                    FieldType.Integer => Decls.Int,
                    FieldType.Decimal => Decls.Double,
                    FieldType.Boolean => Decls.Bool,
                    FieldType.String or FieldType.Regex or FieldType.Uuid or FieldType.Enum => Decls.String,
                    _ => result
                };
            if (booleanResult && !result.Equals(Decls.Bool) && !result.Equals(Decls.Dyn))
                return (null, "Выражение правила должно возвращать логическое значение.");
            var expected = expectedType switch
            {
                FieldType.Integer => Decls.Int,
                FieldType.Decimal => Decls.Double,
                FieldType.Boolean => Decls.Bool,
                FieldType.String or FieldType.Regex or FieldType.Uuid or FieldType.Enum => Decls.String,
                _ => null
            };
            if (expected is not null && !result.Equals(Decls.Dyn) && !result.Equals(expected) &&
                !(expectedType == FieldType.Decimal && result.Equals(Decls.Int)))
                return (null, "Тип результата выражения несовместим с типом поля.");
            return (new CelExpression(source, Array.AsReadOnly(dependencies.ToArray())), null);
        }
        finally { CultureInfo.CurrentCulture = savedCulture; }
    }

    private static bool CheckNode(Expr expr, HashSet<string> dependencies, bool literalList, int depth, out string error)
    {
        error = "";
        if (depth > 64) { error = "Выражение слишком глубоко вложено."; return false; }
        switch (expr.ExprKindCase)
        {
            case Expr.ExprKindOneofCase.IdentExpr:
                dependencies.Add(expr.IdentExpr.Name);
                return true;
            case Expr.ExprKindOneofCase.SelectExpr when !expr.SelectExpr.TestOnly:
                return CheckNode(expr.SelectExpr.Operand, dependencies, false, depth + 1, out error);
            case Expr.ExprKindOneofCase.ConstExpr:
                if (expr.ConstExpr.HasNullValue || expr.ConstExpr.HasBoolValue || expr.ConstExpr.HasInt64Value ||
                    expr.ConstExpr.HasDoubleValue || expr.ConstExpr.HasStringValue) return true;
                error = "Этот тип литерала не поддерживается.";
                return false;
            case Expr.ExprKindOneofCase.ListExpr when literalList:
                foreach (var item in expr.ListExpr.Elements)
                {
                    var literal = item.ExprKindCase == Expr.ExprKindOneofCase.ConstExpr ||
                        (item.ExprKindCase == Expr.ExprKindOneofCase.CallExpr && item.CallExpr.Function == "-_" &&
                         item.CallExpr.Args.Count == 1 && item.CallExpr.Args[0].ExprKindCase == Expr.ExprKindOneofCase.ConstExpr);
                    if (!literal || !CheckNode(item, dependencies, false, depth + 1, out error))
                    { if (error == "") error = "В списке после in допустимы только литералы."; return false; }
                }
                return true;
            case Expr.ExprKindOneofCase.CallExpr:
                var call = expr.CallExpr;
                if (!AllowedCalls.Contains(call.Function)) { error = "Операция или функция не поддерживается: " + call.Function; return false; }
                if (call.Function == "@in" && (call.Args.Count != 2 || call.Args[1].ExprKindCase != Expr.ExprKindOneofCase.ListExpr))
                { error = "После in должен быть список литералов."; return false; }
                if (call.Target is not null && !CheckNode(call.Target, dependencies, false, depth + 1, out error)) return false;
                for (var i = 0; i < call.Args.Count; i++)
                    if (!CheckNode(call.Args[i], dependencies, call.Function == "@in" && i == 1, depth + 1, out error)) return false;
                return true;
            default:
                error = "Конструкция CEL не поддерживается в спецификации.";
                return false;
        }
    }

    private static bool NestingTooDeep(string source)
    {
        var depth = 0;
        char quote = '\0';
        var escaped = false;
        foreach (var c in source)
        {
            if (quote != '\0')
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == quote) quote = '\0';
            }
            else if (c is '\'' or '"') quote = c;
            else if (c is '(' or '[' or '{') { if (++depth > 64) return true; }
            else if (c is ')' or ']' or '}') depth--;
        }
        return false;
    }
}
