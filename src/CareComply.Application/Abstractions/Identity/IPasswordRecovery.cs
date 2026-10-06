using System.Diagnostics;

namespace CareComply.Application.Abstractions.Identity;

public interface IPasswordRecovery
{
    Task<PasswordResetChallenge?> CreateChallengeAsync(
        string email,
        CancellationToken cancellationToken);

    Task<PasswordResetOutcome> ResetPasswordAsync(
        Guid userId,
        string urlSafeToken,
        string newPassword,
        CancellationToken cancellationToken);
}

[DebuggerDisplay("PasswordResetChallenge { UserId={UserId}, Email=[REDACTED], Token=[REDACTED] }")]
public sealed class PasswordResetChallenge(Guid userId, string email, string urlSafeToken)
{
    public Guid UserId { get; } = userId;

    public string Email { get; } = email;

    public string UrlSafeToken { get; } = urlSafeToken;

    public override string ToString() =>
        $"PasswordResetChallenge {{ UserId = {UserId}, Email = [REDACTED], Token = [REDACTED] }}";
}

public enum PasswordResetOutcome
{
    Succeeded,
    Invalid,
}
