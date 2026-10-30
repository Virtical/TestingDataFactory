using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using TestingDataFactory.Api.Controllers;
using TestingDataFactory.Users;

namespace TestingDataFactory.Api.Errors;

/// <summary>Превращает любое исключение при обработке запроса в ответ формата <c>{ error: ... }</c>.</summary>
public class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        var (status, response) = exception switch
        {
            ApiException api => (api.StatusCode, ErrorResponse.Create(api.Code, api.Message)),
            EmailTakenException => (StatusCodes.Status409Conflict,
                ErrorResponse.Create(ErrorCode.EmailTaken, "Пользователь с такой почтой уже есть")),
            InvalidCredentialsException => (StatusCodes.Status401Unauthorized,
                ErrorResponse.Create(ErrorCode.InvalidCredentials, "Неверная почта или пароль")),
            ValidationException validation => (StatusCodes.Status400BadRequest, ToResponse(validation)),
            _ => (StatusCodes.Status500InternalServerError,
                ErrorResponse.Create(ErrorCode.InternalError, "Что-то пошло не так, попробуйте позже")),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Необработанная ошибка при запросе {Method} {Path}", context.Request.Method, context.Request.Path);
        }

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(response, cancellationToken);
        return true;
    }

    private static ErrorResponse ToResponse(ValidationException exception)
    {
        var fields = exception.Errors
            .GroupBy(error => JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName))
            .ToDictionary(group => group.Key, group => group.First().ErrorMessage);

        return ErrorResponse.Create(ErrorCode.ValidationError, "Проверьте заполнение полей", fields);
    }
}
