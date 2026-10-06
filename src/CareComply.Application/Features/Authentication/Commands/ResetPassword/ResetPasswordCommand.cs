using System.Diagnostics;
using Mediator;

namespace CareComply.Application.Features.Authentication.Commands.ResetPassword;

[DebuggerDisplay("ResetPasswordCommand { UserId={UserId}, Token=[REDACTED], NewPassword=[REDACTED], ConfirmPassword=[REDACTED] }")]
public sealed class ResetPasswordCommand(
    Guid userId,
    string token,
    string newPassword,
    string confirmPassword) : ICommand<ResetPasswordResult>
{
    public Guid UserId { get; } = userId;

    public string Token { get; } = token;

    public string NewPassword { get; } = newPassword;

    public string ConfirmPassword { get; } = confirmPassword;

    public override string ToString() =>
        $"ResetPasswordCommand {{ UserId = {UserId}, Token = [REDACTED], NewPassword = [REDACTED], ConfirmPassword = [REDACTED] }}";
}
