using FluentValidation;

namespace TestingDataFactory.Api.Controllers.Request;

public sealed record ValidateSpecificationRequest(string Specification);

public sealed class ValidateSpecificationRequestValidator : AbstractValidator<ValidateSpecificationRequest>
{
    public ValidateSpecificationRequestValidator()
    {
        RuleFor(request => request.Specification).NotEmpty().WithMessage("Передайте спецификацию.");
    }
}
