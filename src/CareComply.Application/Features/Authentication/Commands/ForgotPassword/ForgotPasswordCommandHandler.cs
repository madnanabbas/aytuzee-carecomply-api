using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.Authentication.Commands.ForgotPassword;

public sealed class ForgotPasswordCommandHandler(
    IPasswordRecovery passwordRecovery,
    IPasswordRecoveryEmailSender emailSender,
    IPasswordResetLinkFactory linkFactory)
    : ICommandHandler<ForgotPasswordCommand, ForgotPasswordResult>
{
    public async ValueTask<ForgotPasswordResult> Handle(
        ForgotPasswordCommand command,
        CancellationToken cancellationToken)
    {
        var challenge = await passwordRecovery.CreateChallengeAsync(
            command.Email.Trim(),
            cancellationToken);

        if (challenge is not null)
        {
            var resetUrl = linkFactory.Create(challenge.UserId, challenge.UrlSafeToken);
            if (resetUrl is not null)
            {
                await emailSender.SendAsync(challenge.Email, resetUrl, cancellationToken);
            }
        }

        return ForgotPasswordResult.Accepted;
    }
}
