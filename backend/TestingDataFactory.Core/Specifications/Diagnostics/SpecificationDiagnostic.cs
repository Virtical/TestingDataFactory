using System.Text.Json.Serialization;

namespace TestingDataFactory.Core.Specifications.Diagnostics;

/// <summary>Ошибка разбора YAML. Номера строк и столбцов начинаются с единицы.</summary>
public sealed record SpecificationDiagnostic(string Code, string Message, string Path, int? Line = null, int? Column = null)
{
    /// <summary>Допустимые ключи родительского YAML-узла, если диагностика содержит такую подсказку.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? AllowedKeys { get; init; }
}
