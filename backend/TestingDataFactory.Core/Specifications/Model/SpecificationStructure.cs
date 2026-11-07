namespace TestingDataFactory.Core.Specifications.Model;

/// <summary>Верхний уровень документа; содержимое разделов ещё не проверено.</summary>
public sealed record SpecificationStructure(string Version, string Locale, IReadOnlyList<string> Sections);
