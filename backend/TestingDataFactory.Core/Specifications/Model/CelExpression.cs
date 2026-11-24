namespace TestingDataFactory.Core.Specifications.Model;

/// <summary>Проверенное CEL-выражение и используемые им имена.</summary>
public sealed record CelExpression(string Source, IReadOnlyList<string> Dependencies);

public enum RuleSeverity { Error, Warning }

public sealed record RuleDefinition(CelExpression Expr, CelExpression? When, string? Message, RuleSeverity Severity);
