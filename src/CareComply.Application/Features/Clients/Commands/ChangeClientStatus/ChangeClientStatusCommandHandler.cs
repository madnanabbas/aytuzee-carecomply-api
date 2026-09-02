using CareComply.Application.Abstractions.Clients;
using Mediator;

namespace CareComply.Application.Features.Clients.Commands.ChangeClientStatus;

public sealed class ChangeClientStatusCommandHandler(IClientAdministration administration)
    : ICommandHandler<ChangeClientStatusCommand, ChangeClientStatusResult>
{
    public async ValueTask<ChangeClientStatusResult> Handle(
        ChangeClientStatusCommand command,
        CancellationToken cancellationToken) =>
        ChangeClientStatusResult.Completed(await administration.ChangeStatusAsync(
            new(
                command.ClientId,
                command.Status,
                command.DischargeDate,
                command.DischargeReason,
                command.EditorVersion),
            cancellationToken));
}
