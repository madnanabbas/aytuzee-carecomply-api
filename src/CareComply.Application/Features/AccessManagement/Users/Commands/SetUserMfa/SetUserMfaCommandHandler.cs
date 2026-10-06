using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.SetUserMfa;

public sealed class SetUserMfaCommandHandler(IUserMfaAdministration administration)
    : ICommandHandler<SetUserMfaCommand, SetUserMfaResult>
{
    public async ValueTask<SetUserMfaResult> Handle(
        SetUserMfaCommand command,
        CancellationToken cancellationToken) =>
        SetUserMfaResult.Succeeded(await administration.SetAsync(
            command.UserId,
            command.IsEnabled,
            command.Method,
            command.EditorVersion,
            cancellationToken));
}
