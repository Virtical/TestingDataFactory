namespace TestingDataFactory.Core.Specifications.Diagnostics;

/// <summary>Ошибка разбора YAML. Номера строк и столбцов начинаются с единицы.</summary>
public sealed record SpecificationDiagnostic(string Code, string Message, string Path, int? Line = null, int? Column = null);
