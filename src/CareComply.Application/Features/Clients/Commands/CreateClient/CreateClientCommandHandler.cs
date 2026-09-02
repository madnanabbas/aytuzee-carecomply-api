using CareComply.Application.Abstractions.Clients;
using Mediator;

namespace CareComply.Application.Features.Clients.Commands.CreateClient;

public sealed class CreateClientCommandHandler(IClientAdministration administration)
    : ICommandHandler<CreateClientCommand, CreateClientResult>
{
    public async ValueTask<CreateClientResult> Handle(
        CreateClientCommand command,
        CancellationToken cancellationToken) =>
        CreateClientResult.Completed(await administration.CreateAsync(
            new(
                command.Identity,
                command.Contact,
                command.CareSetup,
                command.Safety,
                command.AdditionalProfile,
                command.InitialStatus,
                command.DischargeDate,
                command.DischargeReason,
                command.Services,
                command.Languages),
            cancellationToken));
}
