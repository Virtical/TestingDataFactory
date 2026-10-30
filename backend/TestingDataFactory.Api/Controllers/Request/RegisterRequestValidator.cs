using FluentValidation;
using TestingDataFactory.Api.Validation;

namespace TestingDataFactory.Api.Controllers.Request;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(r => r.FirstName).PersonName("Введите имя");
        RuleFor(r => r.LastName).PersonName("Введите фамилию");
        RuleFor(r => r.Email).ValidEmail();
        RuleFor(r => r.Password)
            .NotEmpty().WithMessage("Введите пароль")
            .MinimumLength(RuleExtensions.MinPasswordLength)
            .WithMessage($"Пароль должен быть не короче {RuleExtensions.MinPasswordLength} символов");
    }
}
