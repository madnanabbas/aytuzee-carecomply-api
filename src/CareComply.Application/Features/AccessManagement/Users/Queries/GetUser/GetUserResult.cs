using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.Users.Queries.GetUser;

public sealed record GetUserResult(
    UserDirectoryDetails? User,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<GetUserResult>
{
    public static GetUserResult Succeeded(UserDirectoryDetails? user) => new(user, null);
    public static GetUserResult Denied(AuthorizationFailure failure) => new(null, failure);
}
