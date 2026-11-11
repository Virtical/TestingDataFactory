using FluentValidation;
using TestingDataFactory.Api.Validation;

namespace TestingDataFactory.Api.Controllers.Request;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).ValidEmail();
        RuleFor(r => r.Password).NotEmpty().WithMessage("Введите пароль");
    }
}
