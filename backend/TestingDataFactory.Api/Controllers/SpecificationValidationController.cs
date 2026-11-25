using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TestingDataFactory.Api.Controllers.Request;
using TestingDataFactory.Api.Controllers.Response;
using TestingDataFactory.Api.Datasets;
using TestingDataFactory.Core.Specifications;

namespace TestingDataFactory.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/specifications")]
[Consumes("application/json")]
[RequestSizeLimit(5 * 1024 * 1024)]
public sealed class SpecificationValidationController : ControllerBase
{
    /// <summary>Проверяет структуру, связи и параметры генерации по тем же правилам, что и воркер.</summary>
    [HttpPost("validate")]
    public SpecificationValidationResponse Validate([FromBody] ValidateSpecificationRequest request)
    {
        var parser = new SpecificationParser();
        var parsed = parser.Parse(request.Specification);
        return new(parsed.IsSuccess
            ? parser.ValidateGenerationOptions(parsed.Specification!).Diagnostics
            : parsed.Diagnostics);
    }

    /// <summary>Проверяет совместимость спецификации с генератором датасетов без создания строк.</summary>
    [HttpPost("validate-generation")]
    public SpecificationValidationResponse ValidateGeneration([FromBody] GenerateDatasetRequest request) =>
        new(PreparedDataset.Prepare(request.Specification, request.Format).Diagnostics);
}
