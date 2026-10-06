using System.Diagnostics;

namespace CareComply.Api.Contracts.Authentication;

[DebuggerDisplay("ResetPasswordRequest { UserId={UserId}, Token=[REDACTED], NewPassword=[REDACTED], ConfirmPassword=[REDACTED] }")]
public sealed class ResetPasswordRequest
{
    public Guid UserId { get; init; }

    public string? Token { get; init; }

    public string? NewPassword { get; init; }

    public string? ConfirmPassword { get; init; }

    public override string ToString() =>
        $"ResetPasswordRequest {{ UserId = {UserId}, Token = [REDACTED], NewPassword = [REDACTED], ConfirmPassword = [REDACTED] }}";
}
