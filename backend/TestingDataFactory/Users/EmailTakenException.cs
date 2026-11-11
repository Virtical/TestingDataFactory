namespace TestingDataFactory.Users;

/// <summary>Пользователь с такой почтой уже зарегистрирован.</summary>
public class EmailTakenException(string email) : Exception($"Почта {email} уже занята")
{
    public string Email { get; } = email;
}
