using FluentValidation;
using TestingDataFactory.Backend.Validation;

namespace TestingDataFactory.Backend.Controllers.Request;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).ValidEmail();
        RuleFor(r => r.Password).NotEmpty().WithMessage("Введите пароль");
    }
}
