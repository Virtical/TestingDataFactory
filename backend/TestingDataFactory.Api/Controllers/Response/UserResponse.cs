namespace TestingDataFactory.Api.Controllers.Response;

public record UserResponse(UserDto User);

public record UserDto(string FirstName, string LastName, string Email);