using CareComply.Application.Abstractions.AccessManagement;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Commands.CreateRole;

public sealed record CreateManagedRoleCommand(
    Guid? CompanyId,
    string Name,
    string? Description,
    IReadOnlyList<int> PermissionIds)
    : ICommand<CreateManagedRoleResult>, IRequirePermission, IAuditableRequest<CreateManagedRoleResult>
{
    public string RequiredPermission => Permissions.Role.Manage;

    public AuditEvent CreateAuditEvent(CreateManagedRoleResult response) => new(
        "Role.Created",
        "Role",
        response.Mutation?.Role?.Id.ToString("N") ?? "unavailable",
        response.Mutation?.Status == RoleMutationStatus.Succeeded
            ? AuditOutcome.Succeeded
            : AuditOutcome.Failed,
        AuditMetadata.Create(("reasonCode", response.ReasonCode)));
}
