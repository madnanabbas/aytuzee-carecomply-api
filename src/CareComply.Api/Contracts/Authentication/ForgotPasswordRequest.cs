namespace CareComply.Api.Contracts.Authentication;

public sealed class ForgotPasswordRequest
{
    public string? Email { get; init; }

    public override string ToString() => "ForgotPasswordRequest { Email = [REDACTED] }";
}
