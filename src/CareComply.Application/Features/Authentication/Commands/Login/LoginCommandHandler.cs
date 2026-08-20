using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.Authentication.Commands.Login;

public sealed class LoginCommandHandler(IIdentityAuthentication identityAuthentication)
    : ICommandHandler<LoginCommand, LoginResult>
{
    public async ValueTask<LoginResult> Handle(
        LoginCommand command,
        CancellationToken cancellationToken)
    {
        var outcome = await identityAuthentication.AuthenticateAsync(
            command.Email,
            command.Password,
            cancellationToken);

        return new LoginResult(outcome switch
        {
            IdentityAuthenticationOutcome.Succeeded => LoginStatus.Succeeded,
            IdentityAuthenticationOutcome.RequiresTwoFactor => LoginStatus.MfaRequired,
            IdentityAuthenticationOutcome.RequiresMfaEnrollment =>
                LoginStatus.MfaEnrollmentRequired,
            IdentityAuthenticationOutcome.Failed => LoginStatus.InvalidCredentials,
            IdentityAuthenticationOutcome.Unavailable => LoginStatus.Unavailable,
            _ => throw new InvalidOperationException("The Identity authentication outcome is invalid."),
        });
    }
}
