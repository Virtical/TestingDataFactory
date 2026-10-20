namespace TestingDataFactory.Users;

public class User
{
    public Guid Id { get; init; }

    public required string FirstName { get; init; }

    public required string LastName { get; init; }
    
    public required string Email { get; init; }
    
    public string PasswordHash { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public void SetPasswordHash(string passwordHash) => PasswordHash = passwordHash;
}
