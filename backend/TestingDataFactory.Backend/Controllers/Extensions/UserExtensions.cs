using TestingDataFactory.Backend.Controllers.Response;
using TestingDataFactory.Backend.Users;

namespace TestingDataFactory.Backend.Controllers.Extensions;

public static class UserExtensions
{
    public static UserResponse ToResponse(this User user) =>
        new(new UserDto(user.FirstName, user.LastName, user.Email));
}