using CareComply.Application.Abstractions.AccessManagement;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Commands.UpdateRoleGeneral;

public sealed record UpdateManagedRoleGeneralResult(RoleMutationResult? Mutation, AuthorizationFailure? AuthorizationFailure)
{
    public string ReasonCode => AuthorizationFailure?.ToString() ?? Mutation?.Status.ToString() ?? "Unavailable";
    public static UpdateManagedRoleGeneralResult Succeeded(RoleMutationResult mutation) => new(mutation, null);
    public static UpdateManagedRoleGeneralResult Denied(AuthorizationFailure failure) => new(null, failure);
}
