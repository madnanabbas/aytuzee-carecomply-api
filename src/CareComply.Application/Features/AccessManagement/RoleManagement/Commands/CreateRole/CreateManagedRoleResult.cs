using CareComply.Application.Abstractions.AccessManagement;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Commands.CreateRole;

public sealed record CreateManagedRoleResult(RoleMutationResult? Mutation, AuthorizationFailure? AuthorizationFailure)
{
    public string ReasonCode => AuthorizationFailure?.ToString() ?? Mutation?.Status.ToString() ?? "Unavailable";
    public static CreateManagedRoleResult Succeeded(RoleMutationResult mutation) => new(mutation, null);
    public static CreateManagedRoleResult Denied(AuthorizationFailure failure) => new(null, failure);
}
