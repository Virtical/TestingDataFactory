using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using TestingDataFactory.Api.Controllers.Request;
using TestingDataFactory.Core.Specifications;
using TestingDataFactory.Core.Specifications.Diagnostics;
using TestingDataFactory.Core.Specifications.Model;

namespace TestingDataFactory.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/datasets")]
public sealed class DatasetGenerationController : ControllerBase
{
    private const int DefaultRootRows = 100;
    private const int MaxRows = 10_000;
    private const int MaxCells = 250_000;
    private const int MaxFileBytes = 20 * 1024 * 1024;

    [HttpPost("generate")]
    [Consumes("application/json")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Generate([FromBody] GenerateDatasetRequest request, CancellationToken token)
    {
        var parser = new SpecificationParser();
        var parsed = parser.Parse(request.Specification);
        if (!parsed.IsSuccess)
            return InvalidSpecification(parsed.Diagnostics);

        var prepared = parser.ValidateGenerationOptions(parsed.Specification!);
        if (!prepared.IsSuccess)
            return InvalidSpecification(prepared.Diagnostics);

        var spec = prepared.Specification!;
        var options = prepared.Options!;
        var format = request.Format ?? (options.Output switch
        {
            JsonOutput => "json",
            SqlOutput => null,
            _ => "csv"
        });
        if (format is null)
            return BadRequest(ErrorResponse.Create(ErrorCode.ValidationError, "Для генерации данных выберите CSV или JSON."));

        var defaultRows = spec.Tables
            .Where(table => !options.Rows.ContainsKey(table.Key) && table.Value.Fields.Values.All(field => field.Reference is null))
            .ToDictionary(table => table.Key, _ => DefaultRootRows, StringComparer.Ordinal);

        // Core возвращает тот же поток строк независимо от формата скачивания.
        var generated = parser.Generate(parsed.Specification!,
            new GenerationOverrides(Rows: defaultRows, Output: options.Output as CsvOutput ?? new CsvOutput()));
        if (!generated.IsSuccess)
            return InvalidSpecification(generated.Diagnostics);

        if (!FitsRequestLimit(spec, generated.Options!.Rows))
            return BadRequest(ErrorResponse.Create(ErrorCode.ValidationError,
                "За один запуск можно сгенерировать до 10 000 строк и 250 000 ячеек с учётом связей. Уменьшите число строк в спецификации."));

        var zip = format == "csv" && spec.Tables.Count > 1;
        var (contentType, fileName) = format == "json"
            ? ("application/json", "dataset.json")
            : zip ? ("application/zip", "dataset.zip") : ("text/csv; charset=utf-8", "dataset.csv");

        await using var file = new FileBufferingWriteStream(memoryThreshold: 128 * 1024, bufferLimit: MaxFileBytes);
        try
        {
            if (format == "json")
                await generated.WriteJsonAsync(file, token);
            else if (zip)
                await generated.WriteCsvZipAsync(file, token);
            else
                await generated.WriteCsvAsync(file, token);
        }
        catch (InvalidOperationException exception) when (!token.IsCancellationRequested)
        {
            return BadRequest(ErrorResponse.Create(ErrorCode.ValidationError, exception.Message));
        }
        catch (IOException) when (!token.IsCancellationRequested)
        {
            return BadRequest(ErrorResponse.Create(ErrorCode.ValidationError,
                "Не удалось подготовить файл. Уменьшите объём данных. Максимальный размер файла — 20 МБ."));
        }

        Response.ContentType = contentType;
        Response.ContentLength = file.Length;
        Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Generation-Seed"] = generated.Seed!.Value.ToString(CultureInfo.InvariantCulture);
        await file.DrainBufferAsync(Response.Body, token);
        return new EmptyResult();
    }

    private BadRequestObjectResult InvalidSpecification(IReadOnlyList<SpecificationDiagnostic> diagnostics)
    {
        var fields = diagnostics.GroupBy(diagnostic => diagnostic.Path, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => string.Join(" ", group.Select(diagnostic => diagnostic.Message)),
                StringComparer.Ordinal);
        return BadRequest(ErrorResponse.Create(ErrorCode.ValidationError, "Проверьте спецификацию.", fields));
    }

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
