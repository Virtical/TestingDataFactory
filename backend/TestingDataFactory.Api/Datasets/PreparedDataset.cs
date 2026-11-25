using TestingDataFactory.Core.Generation;
using TestingDataFactory.Core.Specifications;
using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Api.Datasets;

internal sealed record PreparedDataset(
    GenerationResult? Generation,
    string? Format,
    int TableCount,
    IReadOnlyList<SpecificationDiagnostic> Diagnostics)
{
    private const int DefaultRootRows = 100;
    private const int MaxRows = 10_000;
    private const int MaxCells = 250_000;

    public static PreparedDataset Prepare(string source, string? requestedFormat)
    {
        var parser = new SpecificationParser();
        var parsed = parser.Parse(source);
        if (!parsed.IsSuccess)
            return Failed(parsed.Diagnostics);

        var prepared = parser.ValidateGenerationOptions(parsed.Specification!);
        if (!prepared.IsSuccess)
            return Failed(prepared.Diagnostics);

        var spec = prepared.Specification!;
        var options = prepared.Options!;
        var format = requestedFormat ?? (options.Output switch
        {
            JsonOutput => "json",
            SqlOutput => null,
            _ => "csv"
        });
        if (format is null)
            return Failed([new("DATASET_FORMAT_REQUIRED", "Для генерации данных выберите CSV или JSON.", "$.generation.output")]);

        var defaultRows = spec.Tables
            .Where(table => !options.Rows.ContainsKey(table.Key) && table.Value.Fields.Values.All(field => field.Reference is null))
            .ToDictionary(table => table.Key, _ => DefaultRootRows, StringComparer.Ordinal);

        // Core подготавливает тот же поток строк для CSV и JSON.
        var generated = parser.Generate(parsed.Specification!,
            new GenerationOverrides(Rows: defaultRows, Output: options.Output as CsvOutput ?? new CsvOutput()));
        if (!generated.IsSuccess)
            return Failed(generated.Diagnostics);

        if (!FitsRequestLimit(spec, generated.Options!.Rows))
            return Failed([new("DATASET_SIZE_LIMIT",
                "За один запуск можно сгенерировать до 10 000 строк и 250 000 ячеек с учётом связей. Уменьшите число строк в спецификации.",
                "$.generation.rows")]);

        return new(generated, format, spec.Tables.Count, []);
    }

    private static PreparedDataset Failed(IReadOnlyList<SpecificationDiagnostic> diagnostics) =>
        new(null, null, 0, diagnostics);

    private static bool FitsRequestLimit(Specification spec, RowCounts rows)
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        long totalRows = 0;
        long totalCells = 0;
        foreach (var table in spec.TableOrder)
        {
            var fields = spec.Tables[table].Fields;
            var reference = fields.Values.FirstOrDefault(field => field.Reference is not null)?.Reference;
            var count = reference is null ? rows[table] : counts[reference.Table] * (reference.PerParent?.Max ?? 1);
            if (count > MaxRows - totalRows)
                return false;

            totalRows += count;
            totalCells += count * fields.Count;
            if (totalCells > MaxCells)
                return false;
            counts.Add(table, count);
        }
        return true;
    }
}
