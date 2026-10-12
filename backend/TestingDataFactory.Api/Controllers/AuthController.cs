using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace TestingDataFactory.Api.Controllers;

// Временная заглушка для фронтенда: пользователи хранятся в памяти и пропадают при перезапуске.
[ApiController]
[Route("api/auth")]
[Consumes("application/json")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, StoredUser> Users = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Регистрация нового пользователя.</summary>
    [HttpPost("register")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public IActionResult Register([FromBody] RegisterRequest? request)
    {
        var fields = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(request?.FirstName)) fields["firstName"] = "Введите имя";
        if (string.IsNullOrWhiteSpace(request?.LastName)) fields["lastName"] = "Введите фамилию";
        ValidateEmail(request?.Email, fields);
        if (string.IsNullOrEmpty(request?.Password)) fields["password"] = "Введите пароль";
        else if (request.Password.Length < 8) fields["password"] = "Пароль должен быть не короче 8 символов";

        if (fields.Count > 0)
            return ValidationError(fields);

        var user = new StoredUser(request!.FirstName!.Trim(), request.LastName!.Trim(), request.Email!.Trim(), request.Password!);
        if (!Users.TryAdd(user.Email, user))
            return Error(StatusCodes.Status409Conflict, "EMAIL_TAKEN", "Пользователь с такой почтой уже есть");

        return StatusCode(StatusCodes.Status201Created, ToResponse(user));
    }

    /// <summary>Вход по почте и паролю.</summary>
    [HttpPost("login")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public IActionResult Login([FromBody] LoginRequest? request)
    {
        var fields = new Dictionary<string, string>();
        ValidateEmail(request?.Email, fields);
        if (string.IsNullOrEmpty(request?.Password)) fields["password"] = "Введите пароль";

        if (fields.Count > 0)
            return ValidationError(fields);

        if (!Users.TryGetValue(request!.Email!.Trim(), out var user) || user.Password != request.Password)
            return Error(StatusCodes.Status401Unauthorized, "INVALID_CREDENTIALS", "Неверная почта или пароль");

        return Ok(ToResponse(user));
    }

    private static void ValidateEmail(string? email, Dictionary<string, string> fields)
    {
        if (string.IsNullOrWhiteSpace(email)) fields["email"] = "Введите почту";
        else if (!System.Net.Mail.MailAddress.TryCreate(email.Trim(), out _)) fields["email"] = "Некорректный адрес почты";
    }

    private ObjectResult ValidationError(Dictionary<string, string> fields) =>
        StatusCode(StatusCodes.Status400BadRequest,
            new ErrorResponse(new ErrorBody("VALIDATION_ERROR", "Проверьте заполнение полей", fields)));

    private ObjectResult Error(int status, string code, string message) =>
        StatusCode(status, new ErrorResponse(new ErrorBody(code, message)));

    private static UserResponse ToResponse(StoredUser user) =>
        new(new UserDto(user.FirstName, user.LastName, user.Email));

    public record RegisterRequest(string? FirstName, string? LastName, string? Email, string? Password);

    public record LoginRequest(string? Email, string? Password);

    public record UserDto(string FirstName, string LastName, string Email);

    public record UserResponse(UserDto User);

    public record ErrorResponse(ErrorBody Error);

    /// <param name="Code">VALIDATION_ERROR, INVALID_CREDENTIALS или EMAIL_TAKEN.</param>
    /// <param name="Message">Общее сообщение, показывается над кнопкой формы.</param>
    /// <param name="Fields">Ошибки по полям, есть только у VALIDATION_ERROR.</param>
    public record ErrorBody(
        string Code,
        string Message,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Dictionary<string, string>? Fields = null);

    private record StoredUser(string FirstName, string LastName, string Email, string Password);
}
