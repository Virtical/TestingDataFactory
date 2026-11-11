using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TestingDataFactory.Api.Errors;

namespace TestingDataFactory.Api.Validation;

/// <summary>
/// Перед вызовом метода контроллера проверяет тело запроса валидатором FluentValidation (если он есть для этого типа).
/// При ошибках бросает <see cref="ValidationException"/>, ответ из него собирает <see cref="ApiExceptionHandler"/>.
/// Подключён глобально в Program.cs.
/// </summary>
public class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var parameter in context.ActionDescriptor.Parameters)
        {
            if (parameter.BindingInfo?.BindingSource != BindingSource.Body)
            {
                continue;
            }

            if (!context.ActionArguments.TryGetValue(parameter.Name, out var body) || body is null)
            {
                throw ApiException.BadRequest();
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(parameter.ParameterType);
            if (services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(new ValidationContext<object>(body), context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                throw new ValidationException(result.Errors);
            }
        }

        await next();
    }
}
