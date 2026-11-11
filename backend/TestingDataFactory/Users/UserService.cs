using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TestingDataFactory.Data;

namespace TestingDataFactory.Users;

/// <summary>
/// Регистрация и вход пользователей. Не знает про HTTP: ошибки сообщает доменными исключениями
/// (<see cref="EmailTakenException"/>, <see cref="InvalidCredentialsException"/>).
/// </summary>
public class UserService(AppDbContext db, IPasswordHasher<User> passwordHasher)
{
    public async Task<User> RegisterAsync(
        string firstName, string lastName, string email, string password, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (await db.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken))
        {
            throw new EmailTakenException(normalizedEmail);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = normalizedEmail,
            CreatedAt = DateTime.UtcNow,
        };
        user.SetPasswordHash(passwordHasher.HashPassword(user, password));

        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new EmailTakenException(normalizedEmail);
        }

        return user;
    }
    
    public async Task<User> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken)
                   ?? throw new InvalidCredentialsException();

        switch (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password))
        {
            case PasswordVerificationResult.Failed:
                throw new InvalidCredentialsException();
            case PasswordVerificationResult.SuccessRehashNeeded:
                user.SetPasswordHash(passwordHasher.HashPassword(user, password));
                await db.SaveChangesAsync(cancellationToken);
                break;
            case PasswordVerificationResult.Success:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return user;
    }

    public async Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.Users.FindAsync([id], cancellationToken);

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
