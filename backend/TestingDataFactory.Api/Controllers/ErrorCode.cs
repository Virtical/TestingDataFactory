using System.Text.Json.Serialization;

namespace TestingDataFactory.Api.Controllers;

public enum ErrorCode
{
    [JsonStringEnumMemberName("UNAUTHORIZED")]
    Unauthorized = 0,
    [JsonStringEnumMemberName("FORBIDDEN")]
    Forbidden = 1,
    [JsonStringEnumMemberName("TOO_MANY_REQUESTS")]
    TooManyRequests = 2,
    [JsonStringEnumMemberName("INTERNAL_ERROR")]
    InternalError = 3,
    [JsonStringEnumMemberName("VALIDATION_ERROR")]
    ValidationError = 10,
    [JsonStringEnumMemberName("EMAIL_TAKEN")]
    EmailTaken = 11,
    [JsonStringEnumMemberName("INVALID_CREDENTIALS")]
    InvalidCredentials = 12
}