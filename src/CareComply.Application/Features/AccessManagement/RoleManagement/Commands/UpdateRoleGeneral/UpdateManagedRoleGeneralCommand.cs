using CareComply.Application.Abstractions.AccessManagement;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Commands.UpdateRoleGeneral;

public sealed record UpdateManagedRoleGeneralCommand(
    Guid RoleId,
    string Name,
    string? Description,
    string EditorVersion)
    : ICommand<UpdateManagedRoleGeneralResult>, IRequirePermission, IAuditableRequest<UpdateManagedRoleGeneralResult>
{
    public string RequiredPermission => Permissions.Role.Manage;

    public AuditEvent CreateAuditEvent(UpdateManagedRoleGeneralResult response) => new(
        "Role.GeneralUpdated",
        "Role",
        RoleId.ToString("N"),
        response.Mutation?.Status is RoleMutationStatus.Succeeded
            or RoleMutationStatus.NoChanges
            ? AuditOutcome.Succeeded
            : AuditOutcome.Failed,
        AuditMetadata.Create(("reasonCode", response.ReasonCode)));
}
