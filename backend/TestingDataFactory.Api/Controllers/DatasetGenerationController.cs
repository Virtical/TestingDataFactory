using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using TestingDataFactory.Api.Controllers.Request;
using TestingDataFactory.Api.Datasets;
using TestingDataFactory.Core.Specifications.Diagnostics;

namespace TestingDataFactory.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/datasets")]
public sealed class DatasetGenerationController : ControllerBase
{
    private const int MaxFileBytes = 20 * 1024 * 1024;

    [HttpPost("generate")]
    [Consumes("application/json")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Generate([FromBody] GenerateDatasetRequest request, CancellationToken token)
    {
        var prepared = PreparedDataset.Prepare(request.Specification, request.Format);
        if (prepared.Generation is not { } generated)
            return InvalidSpecification(prepared.Diagnostics);

        var format = prepared.Format;
        var zip = format == "csv" && prepared.TableCount > 1;
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
        if (diagnostics is [{ Code: "DATASET_FORMAT_REQUIRED" or "DATASET_SIZE_LIMIT" } diagnostic])
            return BadRequest(ErrorResponse.Create(ErrorCode.ValidationError, diagnostic.Message));

        var fields = diagnostics.GroupBy(diagnostic => diagnostic.Path, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => string.Join(" ", group.Select(diagnostic => diagnostic.Message)),
                StringComparer.Ordinal);
        return BadRequest(ErrorResponse.Create(ErrorCode.ValidationError, "Проверьте спецификацию.", fields));
    }

}
