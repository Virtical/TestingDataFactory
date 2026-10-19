using TestingDataFactory.Api.Controllers.Response;
using TestingDataFactory.Users;

namespace TestingDataFactory.Api.Controllers.Extensions;

public static class UserExtensions
{
    public static UserResponse ToResponse(this User user) =>
        new(new UserDto(user.FirstName, user.LastName, user.Email));
}