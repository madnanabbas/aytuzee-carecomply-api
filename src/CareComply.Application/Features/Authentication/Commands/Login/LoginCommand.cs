using System.Diagnostics;
using Mediator;

namespace CareComply.Application.Features.Authentication.Commands.Login;

[DebuggerDisplay("LoginCommand Password=[REDACTED]")]
public sealed class LoginCommand(string email, string password) : ICommand<LoginResult>
{
    public string Email { get; } = email;

    public string Password { get; } = password;

    public override string ToString() =>
        "LoginCommand { Password = [REDACTED] }";
}
