using CareComply.Application.Abstractions.Authentication;
using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.SetUserStatus;

public sealed class SetUserStatusCommandHandler(
    IUserStatusAdministration administration,
    ICurrentActor currentActor)
    : ICommandHandler<SetUserStatusCommand, SetUserStatusResult>
{
    public async ValueTask<SetUserStatusResult> Handle(
        SetUserStatusCommand command,
        CancellationToken cancellationToken)
    {
        if (!command.IsActive && currentActor.UserId == command.UserId)
        {
            return SetUserStatusResult.SelfDeactivation();
        }

        return SetUserStatusResult.Succeeded(await administration.SetActiveAsync(
            command.UserId,
            command.IsActive,
            command.EditorVersion,
            cancellationToken));
    }
}
