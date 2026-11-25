using FluentValidation;

namespace TestingDataFactory.Api.Controllers.Request;

public sealed class GenerateSpecificationRequestValidator : AbstractValidator<GenerateSpecificationRequest>
{
    public GenerateSpecificationRequestValidator()
    {
        RuleFor(request => request.Prompt).NotEmpty().WithMessage("Опишите, какие данные вам нужны");
    }
}
