using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.ReplaceUserRoles;

public sealed class ReplaceUserRolesAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<ReplaceUserRolesResult>
{
    public ReplaceUserRolesResult Create(AuthorizationFailure failure) =>
        ReplaceUserRolesResult.Denied(failure);
}
