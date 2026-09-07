using CareComply.Application.Abstractions.AccessManagement;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.RoleManagement.Commands.CreateRole;

public sealed class CreateManagedRoleCommandHandler(IRoleManagement roles)
    : ICommandHandler<CreateManagedRoleCommand, CreateManagedRoleResult>
{
    public async ValueTask<CreateManagedRoleResult> Handle(CreateManagedRoleCommand command, CancellationToken cancellationToken) =>
        CreateManagedRoleResult.Succeeded(await roles.CreateAsync(
            command.CompanyId,
            command.Name,
            command.Description,
            command.PermissionIds,
            cancellationToken));
}
