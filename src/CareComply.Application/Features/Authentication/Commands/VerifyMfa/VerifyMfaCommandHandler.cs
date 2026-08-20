using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.Authentication.Commands.VerifyMfa;

public sealed class VerifyMfaCommandHandler(IIdentityMfa identityMfa)
    : ICommandHandler<VerifyMfaCommand, MfaSignInStatus>
{
    public async ValueTask<MfaSignInStatus> Handle(
        VerifyMfaCommand command,
        CancellationToken cancellationToken) =>
        await identityMfa.VerifyAuthenticatorAsync(command.Code, cancellationToken);
}
