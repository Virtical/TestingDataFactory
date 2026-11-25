using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestingDataFactory.Api.Controllers.Request;
using TestingDataFactory.Api.Controllers.Response;
using TestingDataFactory.Api.Errors;
using TestingDataFactory.Specifications;

namespace TestingDataFactory.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/specification-jobs")]
[Produces("application/json")]
public sealed class SpecificationJobsController(SpecificationJobService jobs) : ControllerBase
{
    /// <summary>Ставит генерацию YAML-спецификации по описанию в очередь.</summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType<SpecificationJobResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SpecificationJobResponse>> Start(
        [FromBody] GenerateSpecificationRequest request, CancellationToken cancellationToken)
    {
        var job = await jobs.StartAsync(GetUserId(), request.Prompt.Trim(), cancellationToken);
        return AcceptedAtAction(nameof(Get), new { jobId = job.JobId }, ToResponse(job));
    }

    /// <summary>Возвращает состояние и результат генерации владельцу задачи.</summary>
    [HttpGet("{jobId:guid}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<SpecificationJobResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SpecificationJobResponse>> Get(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await jobs.FindAsync(jobId, GetUserId(), cancellationToken);
        if (job is null)
            return NotFound(ErrorResponse.Create(ErrorCode.SpecificationJobNotFound, "Черновик не найден"));

        return Ok(ToResponse(job));
    }

    private Guid GetUserId()
    {
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) && userId != Guid.Empty)
            return userId;

        throw new ApiException(StatusCodes.Status401Unauthorized, ErrorCode.Unauthorized, "Требуется вход в систему");
    }

    private static SpecificationJobResponse ToResponse(SpecificationJob job) =>
        new(job.JobId, job.Status, job.SpecificationYaml, job.ErrorCode, job.ErrorMessage);
}
