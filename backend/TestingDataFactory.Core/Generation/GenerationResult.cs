using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Core.Generation;

public sealed record GeneratedRow(string Table, IReadOnlyDictionary<string, object?> Values);

/// <summary>Результат подготовки; при успехе строки создаются по мере перечисления.</summary>
public sealed record GenerationResult(GenerationOptions? Options, IEnumerable<GeneratedRow>? Rows, IReadOnlyList<SpecificationDiagnostic> Diagnostics)
{
    public bool IsSuccess => Options is not null && Rows is not null && Diagnostics.Count == 0;
    public int? Seed => Options?.Seed;
    internal IReadOnlyList<string>? Columns { get; init; }
    internal IReadOnlyDictionary<string, IReadOnlyList<string>>? TableColumns { get; init; }

    /// <summary>Записывает CSV во временный файл и перемещает его на место после успешной генерации. Существующий файл не заменяется.</summary>
    public Task WriteCsvAsync(string path, CancellationToken cancellationToken = default) =>
        CsvFileWriter.WriteAsync(this, path, cancellationToken);

    /// <summary>Записывает отдельные CSV для всех таблиц в новую папку.</summary>
    public Task WriteCsvDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        CsvFileWriter.WriteDirectoryAsync(this, path, cancellationToken);
}
