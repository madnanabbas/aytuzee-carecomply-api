using CareComply.Application.Abstractions.Clients;
using Mediator;

namespace CareComply.Application.Features.Clients.Commands.UpdateClient;

public sealed class UpdateClientCommandHandler(IClientAdministration administration)
    : ICommandHandler<UpdateClientCommand, UpdateClientResult>
{
    public async ValueTask<UpdateClientResult> Handle(
        UpdateClientCommand command,
        CancellationToken cancellationToken) =>
        UpdateClientResult.Completed(await administration.UpdateAsync(
            new(
                command.ClientId,
                command.Identity,
                command.Contact,
                command.CareSetup,
                command.Safety,
                command.AdditionalProfile,
                command.Services,
                command.Languages,
                command.ServiceChangeEffectiveDate,
                command.EditorVersion),
            cancellationToken));
}
