using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TestingDataFactory.Data;
using TestingDataFactory.Users;

namespace TestingDataFactory.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(AppDbContext db, IPasswordHasher<User> passwordHasher) : ControllerBase
{
    private const int MaxNameLength = 100;
    private const int MaxEmailLength = 256;

    /// <summary>Регистрация нового пользователя. После успешной регистрации пользователь сразу входит в систему.</summary>
    [HttpPost("register")]
    [Consumes("application/json")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest? request)
    {
        var fields = new Dictionary<string, string>();
        ValidateName(request?.FirstName, "firstName", "Введите имя", fields);
        ValidateName(request?.LastName, "lastName", "Введите фамилию", fields);
        ValidateEmail(request?.Email, fields);
        if (string.IsNullOrEmpty(request?.Password)) fields["password"] = "Введите пароль";
        else if (request.Password.Length < 8) fields["password"] = "Пароль должен быть не короче 8 символов";

        if (fields.Count > 0)
            return ValidationError(fields);

        var email = NormalizeEmail(request!.Email!);
        if (await db.Users.AnyAsync(u => u.Email == email))
            return EmailTaken();

        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName!.Trim(),
            LastName = request.LastName!.Trim(),
            Email = email,
            CreatedAt = DateTime.UtcNow,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password!);

        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return EmailTaken();
        }

        await SignInAsync(user);
        return StatusCode(StatusCodes.Status201Created, ToResponse(user));
    }

    /// <summary>Вход по почте и паролю.</summary>
    [HttpPost("login")]
    [Consumes("application/json")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest? request)
    {
        var fields = new Dictionary<string, string>();
        ValidateEmail(request?.Email, fields);
        if (string.IsNullOrEmpty(request?.Password)) fields["password"] = "Введите пароль";

        if (fields.Count > 0)
            return ValidationError(fields);

        var email = NormalizeEmail(request!.Email!);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);
        if (user is null)
            return InvalidCredentials();

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password!);
        if (result == PasswordVerificationResult.Failed)
            return InvalidCredentials();

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password!);
            await db.SaveChangesAsync();
        }

        await SignInAsync(user);
        return Ok(ToResponse(user));
    }

    /// <summary>Выход: удаляет cookie сессии. Можно вызывать и без входа.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    /// <summary>Текущий пользователь по cookie сессии.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me()
    {
        var user = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? await db.Users.FindAsync(id)
            : null;

        if (user is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Unauthorized(ErrorResponse.Create("UNAUTHORIZED", "Требуется вход в систему"));
        }

        return Ok(ToResponse(user));
    }

    private Task SignInAsync(User user)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())],
            CookieAuthenticationDefaults.AuthenticationScheme);

        return HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }

    private static void ValidateName(string? value, string field, string emptyMessage, Dictionary<string, string> fields)
    {
        if (string.IsNullOrWhiteSpace(value)) fields[field] = emptyMessage;
        else if (value.Trim().Length > MaxNameLength) fields[field] = $"Не длиннее {MaxNameLength} символов";
    }

    private static void ValidateEmail(string? email, Dictionary<string, string> fields)
    {
        if (string.IsNullOrWhiteSpace(email))
            fields["email"] = "Введите почту";
        else if (!System.Net.Mail.MailAddress.TryCreate(email.Trim(), out var address)
                 || address.Address != email.Trim()
                 || email.Trim().Length > MaxEmailLength)
            fields["email"] = "Некорректный адрес почты";
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private ObjectResult ValidationError(Dictionary<string, string> fields) =>
        BadRequest(ErrorResponse.Create("VALIDATION_ERROR", "Проверьте заполнение полей", fields));

    private ObjectResult EmailTaken() =>
        Conflict(ErrorResponse.Create("EMAIL_TAKEN", "Пользователь с такой почтой уже есть"));

    private ObjectResult InvalidCredentials() =>
        Unauthorized(ErrorResponse.Create("INVALID_CREDENTIALS", "Неверная почта или пароль"));

    private static UserResponse ToResponse(User user) =>
        new(new UserDto(user.FirstName, user.LastName, user.Email));

    public record RegisterRequest(string? FirstName, string? LastName, string? Email, string? Password);

    public record LoginRequest(string? Email, string? Password);

    public record UserDto(string FirstName, string LastName, string Email);

    public record UserResponse(UserDto User);
}
