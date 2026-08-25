using System.Diagnostics;

namespace CareComply.Application.Abstractions.Identity;

public interface IIdentityMfa
{
    Task<MfaContext> GetContextAsync(CancellationToken cancellationToken);

    Task<MfaEnrollmentData?> BeginEnrollmentAsync(CancellationToken cancellationToken);

    Task<MfaEnrollmentConfirmation> ConfirmEnrollmentAsync(
        string code,
        CancellationToken cancellationToken);

    Task<MfaSignInStatus> VerifyAuthenticatorAsync(
        string code,
        CancellationToken cancellationToken);

    Task<MfaSignInStatus> VerifyRecoveryCodeAsync(
        string recoveryCode,
        CancellationToken cancellationToken);

    Task<MfaEmailSendStatus> ResendEmailCodeAsync(CancellationToken cancellationToken);

    Task CancelAsync(CancellationToken cancellationToken);
}

public sealed record MfaContext(MfaContextKind Kind, UserMfaMethod Method);

public enum MfaContextKind
{
    None,
    VerificationRequired,
    EnrollmentRequired,
}

public enum MfaSignInStatus
{
    Succeeded,
    Invalid,
    Expired,
    Unavailable,
}

public enum MfaEmailSendStatus
{
    Succeeded,
    Expired,
    Unavailable,
}

[DebuggerDisplay("MfaEnrollmentData { ManualSetupKey=[REDACTED], AuthenticatorUri=[REDACTED] }")]
public sealed class MfaEnrollmentData
{
    public MfaEnrollmentData(string manualSetupKey, string authenticatorUri)
    {
        ManualSetupKey = manualSetupKey;
        AuthenticatorUri = authenticatorUri;
    }

    public string ManualSetupKey { get; }

    public string AuthenticatorUri { get; }

    public override string ToString() =>
        "MfaEnrollmentData { ManualSetupKey = [REDACTED], AuthenticatorUri = [REDACTED] }";
}

[DebuggerDisplay("MfaEnrollmentConfirmation { Status={Status}, RecoveryCodes=[REDACTED] }")]
public sealed class MfaEnrollmentConfirmation
{
    public MfaEnrollmentConfirmation(
        MfaSignInStatus status,
        IReadOnlyList<string> recoveryCodes)
    {
        Status = status;
        RecoveryCodes = recoveryCodes;
    }

    public MfaSignInStatus Status { get; }

    public IReadOnlyList<string> RecoveryCodes { get; }

    public static MfaEnrollmentConfirmation Failed(MfaSignInStatus status) =>
        new(status, []);

    public override string ToString() =>
        $"MfaEnrollmentConfirmation {{ Status = {Status}, RecoveryCodes = [REDACTED] }}";
}
