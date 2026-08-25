using Mediator;

namespace CareComply.Application.Features.Authentication.Commands.ForgotPassword;

public sealed class ForgotPasswordCommand(string email) : ICommand<ForgotPasswordResult>
{
    public string Email { get; } = email;

    public override string ToString() => "ForgotPasswordCommand { Email = [REDACTED] }";
}
