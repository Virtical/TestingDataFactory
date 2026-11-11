using System.Net.Mail;
using FluentValidation;

namespace TestingDataFactory.Api.Validation;

/// <summary>Общие правила, которые используются в нескольких валидаторах.</summary>
public static class RuleExtensions
{
    public const int MaxNameLength = 100;
    public const int MaxEmailLength = 256;
    public const int MinPasswordLength = 8;

    public static IRuleBuilderOptions<T, string> PersonName<T>(this IRuleBuilder<T, string> rule, string emptyMessage) =>
        rule
            .NotEmpty().WithMessage(emptyMessage)
            .Must(value => value.Trim().Length <= MaxNameLength).WithMessage($"Не длиннее {MaxNameLength} символов");

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("Введите почту")
            .Must(IsValidEmail).WithMessage("Некорректный адрес почты");

    private static bool IsValidEmail(string email)
    {
        var trimmed = email.Trim();
        return trimmed.Length <= MaxEmailLength
               && MailAddress.TryCreate(trimmed, out var address)
               && address.Address == trimmed;
    }
}
