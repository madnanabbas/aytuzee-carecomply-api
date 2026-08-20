using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.Authentication.Commands.CancelMfaChallenge;

public sealed class CancelMfaChallengeCommandHandler(IIdentityMfa identityMfa)
    : ICommandHandler<CancelMfaChallengeCommand>
{
    public async ValueTask<Unit> Handle(
        CancelMfaChallengeCommand command,
        CancellationToken cancellationToken)
    {
        await identityMfa.CancelAsync(cancellationToken);
        return Unit.Value;
    }
}
