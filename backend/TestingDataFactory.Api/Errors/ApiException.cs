using TestingDataFactory.Api.Controllers;

namespace TestingDataFactory.Api.Errors;

/// <summary>
/// Ожидаемая ошибка, которую нужно отдать клиенту: бросается из любого места обработки запроса,
/// в ответ превращается в <see cref="ApiExceptionHandler"/>.
/// </summary>
public class ApiException(int statusCode, ErrorCode code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public ErrorCode Code { get; } = code;

    public static ApiException BadRequest() =>
        new(StatusCodes.Status400BadRequest, ErrorCode.ValidationError, "Некорректный запрос");
}
