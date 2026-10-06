using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.ReplaceUserRoles;

public sealed class ReplaceUserRolesCommandHandler(IUserRoleAdministration administration)
    : ICommandHandler<ReplaceUserRolesCommand, ReplaceUserRolesResult>
{
    public async ValueTask<ReplaceUserRolesResult> Handle(
        ReplaceUserRolesCommand command,
        CancellationToken cancellationToken) =>
        ReplaceUserRolesResult.Succeeded(await administration.ReplaceAsync(
            command.UserId,
            command.RoleIds,
            command.EditorVersion,
            cancellationToken));
}
