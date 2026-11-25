using FluentValidation;

namespace TestingDataFactory.Api.Controllers.Request;

public sealed record GenerateDatasetRequest(string Specification, string? Format = null);

public sealed class GenerateDatasetRequestValidator : AbstractValidator<GenerateDatasetRequest>
{
    public GenerateDatasetRequestValidator()
    {
        RuleFor(request => request.Specification).NotEmpty().WithMessage("Передайте спецификацию.");
        RuleFor(request => request.Format)
            .Must(format => format is null or "csv" or "json").WithMessage("Выберите CSV или JSON.");
    }
}
