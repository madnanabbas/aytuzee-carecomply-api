using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.Authentication.Commands.BeginMfaEnrollment;

public sealed class BeginMfaEnrollmentCommandHandler(IIdentityMfa identityMfa)
    : ICommandHandler<BeginMfaEnrollmentCommand, MfaEnrollmentData?>
{
    public async ValueTask<MfaEnrollmentData?> Handle(
        BeginMfaEnrollmentCommand command,
        CancellationToken cancellationToken) =>
        await identityMfa.BeginEnrollmentAsync(cancellationToken);
}
