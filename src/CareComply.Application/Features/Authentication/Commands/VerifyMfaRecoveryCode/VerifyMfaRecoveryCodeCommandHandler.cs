using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.Authentication.Commands.VerifyMfaRecoveryCode;

public sealed class VerifyMfaRecoveryCodeCommandHandler(IIdentityMfa identityMfa)
    : ICommandHandler<VerifyMfaRecoveryCodeCommand, MfaSignInStatus>
{
    public async ValueTask<MfaSignInStatus> Handle(
        VerifyMfaRecoveryCodeCommand command,
        CancellationToken cancellationToken) =>
        await identityMfa.VerifyRecoveryCodeAsync(command.RecoveryCode, cancellationToken);
}
