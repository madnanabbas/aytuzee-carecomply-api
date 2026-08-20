using System.Diagnostics;

namespace CareComply.Api.Contracts.Authentication;

[DebuggerDisplay("LoginRequest Password=[REDACTED]")]
public sealed class LoginRequest
{
    public string? Email { get; init; }

    public string? Password { get; init; }

    public override string ToString() =>
        "LoginRequest { Password = [REDACTED] }";
}
