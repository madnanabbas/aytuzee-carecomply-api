using CareComply.Application.Abstractions.AccessManagement;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Commands.UpdateRoleGeneral;

public sealed class UpdateManagedRoleGeneralCommandHandler(IRoleManagement roles)
    : ICommandHandler<UpdateManagedRoleGeneralCommand, UpdateManagedRoleGeneralResult>
{
    public async ValueTask<UpdateManagedRoleGeneralResult> Handle(UpdateManagedRoleGeneralCommand command, CancellationToken cancellationToken) =>
        UpdateManagedRoleGeneralResult.Succeeded(await roles.UpdateGeneralAsync(
            command.RoleId,
            command.Name,
            command.Description,
            command.EditorVersion,
            cancellationToken));
}
