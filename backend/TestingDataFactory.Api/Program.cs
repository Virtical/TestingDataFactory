using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TestingDataFactory.Api;
using TestingDataFactory.Api.Controllers;
using TestingDataFactory.Api.Errors;
using TestingDataFactory.Api.Validation;
using TestingDataFactory.Data;
using TestingDataFactory.Users;

var builder = WebApplication.CreateBuilder(args);

// Ошибки отдаём сами в формате { error: ... }, а не стандартным ProblemDetails:
// тело запроса проверяет ValidationFilter с помощью валидаторов FluentValidation,
// а исключения (ошибки валидации, ApiException и все остальные) превращает в ответ ApiExceptionHandler.
builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>())
    .ConfigureApiBehaviorOptions(options => options.SuppressModelStateInvalidFilter = true)
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddOpenApi();

builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
// Правила поля выполняются до первой ошибки: на поле одно сообщение, и правила после NotEmpty не получают null.
ValidatorOptions.Global.DefaultRuleLevelCascadeMode = CascadeMode.Stop;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<UserService>();

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
            context.HttpContext, StatusCodes.Status401Unauthorized, ErrorCode.Unauthorized, "Требуется вход в систему");
        options.Events.OnRedirectToAccessDenied = context => WriteError(
            context.HttpContext, StatusCodes.Status403Forbidden, ErrorCode.Forbidden, "Недостаточно прав");
    });
builder.Services.AddAuthorization();

// Защита от перебора паролей: не больше 10 попыток входа/регистрации в минуту с одного IP.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(RateLimitPolicies.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));

    options.OnRejected = (context, _) => new ValueTask(WriteError(
        context.HttpContext, StatusCodes.Status429TooManyRequests, ErrorCode.TooManyRequests,
        "Слишком много попыток, подождите минуту"));
});

var app = builder.Build();

// Первым в конвейере, чтобы ловить исключения из всех следующих middleware и контроллеров.
// Ответ формирует ApiExceptionHandler; пустой запасной обработчик нужен только потому, что без него
// (или без AddProblemDetails, который мы не используем) UseExceptionHandler падает при старте.
app.UseExceptionHandler(_ => { });

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

static Task WriteError(HttpContext context, int status, ErrorCode code, string message)
{
    context.Response.StatusCode = status;
    return context.Response.WriteAsJsonAsync(ErrorResponse.Create(code, message));
}
