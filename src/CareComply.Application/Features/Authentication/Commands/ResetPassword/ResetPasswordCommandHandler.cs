using CareComply.Application.Abstractions.Identity;
using Mediator;

namespace CareComply.Application.Features.Authentication.Commands.ResetPassword;

public sealed class ResetPasswordCommandHandler(IPasswordRecovery passwordRecovery)
    : ICommandHandler<ResetPasswordCommand, ResetPasswordResult>
{
    public async ValueTask<ResetPasswordResult> Handle(
        ResetPasswordCommand command,
        CancellationToken cancellationToken)
    {
        var outcome = await passwordRecovery.ResetPasswordAsync(
            command.UserId,
            command.Token,
            command.NewPassword,
            cancellationToken);

        return new(outcome == PasswordResetOutcome.Succeeded
            ? ResetPasswordStatus.Succeeded
            : ResetPasswordStatus.Invalid);
    }
}
