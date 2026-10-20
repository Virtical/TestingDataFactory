using System.Text.Json.Serialization;
using TestingDataFactory.Api.Controllers;

namespace TestingDataFactory.Api;

/// <summary>Единый формат ошибок API: <c>{ error: { code, message, fields? } }</c>.</summary>
public record ErrorResponse(ErrorBody Error)
{
    public static ErrorResponse Create(ErrorCode code, string message, Dictionary<string, string> fields) =>
        new(new ErrorBody(code, message, fields));
    
    public static ErrorResponse Create(ErrorCode code, string message) =>
        new(new ErrorBody(code, message));
}

/// <param name="Code">
/// VALIDATION_ERROR, INVALID_CREDENTIALS, EMAIL_TAKEN, UNAUTHORIZED, FORBIDDEN, TOO_MANY_REQUESTS или INTERNAL_ERROR.
/// </param>
/// <param name="Message">Общее сообщение, показывается над кнопкой формы.</param>
/// <param name="Fields">Ошибки по полям (ключи — имена полей запроса в camelCase), есть только у VALIDATION_ERROR.</param>
public record ErrorBody(
    ErrorCode Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Dictionary<string, string>? Fields = null);
