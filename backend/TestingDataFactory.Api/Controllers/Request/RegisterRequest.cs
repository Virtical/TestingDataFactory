namespace TestingDataFactory.Api.Controllers.Request;

public record RegisterRequest(string FirstName, string LastName, string Email, string Password);