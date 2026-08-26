using CareComply.Application.Abstractions.AccessManagement;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Commands.ReplaceRolePermissions;

public sealed class ReplaceManagedRolePermissionsCommandHandler(IRoleManagement roles)
    : ICommandHandler<ReplaceManagedRolePermissionsCommand, ReplaceManagedRolePermissionsResult>
{
    public async ValueTask<ReplaceManagedRolePermissionsResult> Handle(
        ReplaceManagedRolePermissionsCommand command,
        CancellationToken cancellationToken) =>
        ReplaceManagedRolePermissionsResult.Succeeded(await roles.ReplacePermissionsAsync(
            command.RoleId,
            command.PermissionIds,
            command.EditorVersion,
            cancellationToken));
}
