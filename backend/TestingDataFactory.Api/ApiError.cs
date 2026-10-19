using System.Text.Json.Serialization;

namespace TestingDataFactory.Api;

/// <summary>Единый формат ошибок API: <c>{ error: { code, message, fields? } }</c>.</summary>
public record ErrorResponse(ErrorBody Error)
{
    public static ErrorResponse Create(string code, string message, Dictionary<string, string>? fields = null) =>
        new(new ErrorBody(code, message, fields));
}

/// <param name="Code">
/// VALIDATION_ERROR, INVALID_CREDENTIALS, EMAIL_TAKEN, UNAUTHORIZED, FORBIDDEN или TOO_MANY_REQUESTS.
/// </param>
/// <param name="Message">Общее сообщение, показывается над кнопкой формы.</param>
/// <param name="Fields">Ошибки по полям, есть только у VALIDATION_ERROR.</param>
public record ErrorBody(
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Dictionary<string, string>? Fields = null);
