using CareComply.Application.Abstractions.AccessManagement;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Commands.ReplaceRolePermissions;

public sealed record ReplaceManagedRolePermissionsResult(
    RolePermissionMutationResult? Mutation,
    AuthorizationFailure? AuthorizationFailure)
{
    public string ReasonCode => AuthorizationFailure?.ToString() ?? Mutation?.Status.ToString() ?? "Unavailable";
    public static ReplaceManagedRolePermissionsResult Succeeded(RolePermissionMutationResult mutation) => new(mutation, null);
    public static ReplaceManagedRolePermissionsResult Denied(AuthorizationFailure failure) => new(null, failure);
}
