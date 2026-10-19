using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TestingDataFactory.Api;
using TestingDataFactory.Data;
using TestingDataFactory.Users;

var builder = WebApplication.CreateBuilder(args);

// Ошибки валидации отдаём сами в формате { error: ... }, а не стандартным ProblemDetails.
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options => options.SuppressModelStateInvalidFilter = true);
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "dataforge.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // Флаг Secure ставится, если запрос пришёл по HTTPS. В продакшене API должен работать только по HTTPS.
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;

        // По умолчанию cookie-авторизация перенаправляет на страницу входа. API вместо этого отвечает 401/403.
        options.Events.OnRedirectToLogin = context => WriteError(
            context.HttpContext, StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Требуется вход в систему");
        options.Events.OnRedirectToAccessDenied = context => WriteError(
            context.HttpContext, StatusCodes.Status403Forbidden, "FORBIDDEN", "Недостаточно прав");
    });
builder.Services.AddAuthorization();

// Защита от перебора паролей: не больше 10 попыток входа/регистрации в минуту с одного IP.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(RateLimitPolicies.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));

    options.OnRejected = (context, _) => new ValueTask(WriteError(
        context.HttpContext, StatusCodes.Status429TooManyRequests, "TOO_MANY_REQUESTS",
        "Слишком много попыток, подождите минуту"));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "TestingDataFactory API"));

    // В разработке база создаётся и обновляется до последней миграции при запуске.
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.Run();

static Task WriteError(HttpContext context, int status, string code, string message)
{
    context.Response.StatusCode = status;
    return context.Response.WriteAsJsonAsync(ErrorResponse.Create(code, message));
}
