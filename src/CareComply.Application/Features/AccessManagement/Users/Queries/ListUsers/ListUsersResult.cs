using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.Users.Queries.ListUsers;

public sealed record ListUsersResult(
    UserDirectoryPage? Page,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<ListUsersResult>
{
    public static ListUsersResult Succeeded(UserDirectoryPage page) => new(page, null);

    public static ListUsersResult Denied(AuthorizationFailure failure) => new(null, failure);
}
