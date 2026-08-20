using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.Authentication.Commands.ConfirmMfaEnrollment;

public sealed class ConfirmMfaEnrollmentCommandHandler(IIdentityMfa identityMfa)
    : ICommandHandler<ConfirmMfaEnrollmentCommand, MfaEnrollmentConfirmation>
{
    public async ValueTask<MfaEnrollmentConfirmation> Handle(
        ConfirmMfaEnrollmentCommand command,
        CancellationToken cancellationToken) =>
        await identityMfa.ConfirmEnrollmentAsync(command.Code, cancellationToken);
}
