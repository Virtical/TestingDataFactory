using TestingDataFactory.Specifications;

namespace TestingDataFactory.Api.Controllers.Response;

public record SpecificationJobResponse(
    Guid JobId,
    SpecificationJobStatus Status,
    string? SpecificationYaml,
    string? ErrorCode,
    string? ErrorMessage);
