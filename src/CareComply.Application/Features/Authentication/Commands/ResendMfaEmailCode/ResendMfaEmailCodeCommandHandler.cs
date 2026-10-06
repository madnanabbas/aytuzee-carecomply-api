using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.Authentication.Commands.ResendMfaEmailCode;

public sealed class ResendMfaEmailCodeCommandHandler(IIdentityMfa identityMfa)
    : ICommandHandler<ResendMfaEmailCodeCommand, MfaEmailSendStatus>
{
    public async ValueTask<MfaEmailSendStatus> Handle(
        ResendMfaEmailCodeCommand command,
        CancellationToken cancellationToken) =>
        await identityMfa.ResendEmailCodeAsync(cancellationToken);
}
