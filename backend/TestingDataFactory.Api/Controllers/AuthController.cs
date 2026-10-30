using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TestingDataFactory.Api.Controllers.Extensions;
using TestingDataFactory.Api.Controllers.Request;
using TestingDataFactory.Api.Controllers.Response;
using TestingDataFactory.Users;

namespace TestingDataFactory.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(UserService userService) : ControllerBase
{
#region register
    /// <summary>Регистрация нового пользователя. После успешной регистрации пользователь сразу входит в систему.</summary>
    [HttpPost("register")]
    [Consumes("application/json")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = await userService.RegisterAsync(
            request.FirstName, request.LastName, request.Email, request.Password, cancellationToken);

        await SignInAsync(user);
        return StatusCode(StatusCodes.Status201Created, user.ToResponse());
    }
#endregion register

#region login
    /// <summary>Вход по почте и паролю.</summary>
    [HttpPost("login")]
    [Consumes("application/json")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await userService.AuthenticateAsync(request.Email, request.Password, cancellationToken);

        await SignInAsync(user);
        return Ok(user.ToResponse());
    }
#endregion login

#region logout
    /// <summary>Выход: удаляет cookie сессии. Можно вызывать и без входа.</summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
#endregion logout

#region me
    /// <summary>Текущий пользователь по cookie сессии.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var user = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? await userService.FindByIdAsync(id, cancellationToken)
            : null;

        if (user is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Unauthorized(ErrorResponse.Create(ErrorCode.Unauthorized, "Требуется вход в систему"));
        }

        return Ok(user.ToResponse());
    }
#endregion me

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
}
