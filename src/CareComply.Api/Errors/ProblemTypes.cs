namespace CareComply.Api.Errors;

public static class ProblemTypes
{
    public const string AccessDenied = "urn:carecomply:problem:access-denied";
    public const string AntiforgeryValidation = "urn:carecomply:problem:antiforgery-validation";
    public const string AuthenticationFailed = "urn:carecomply:problem:authentication-failed";
    public const string AuthenticationRequired = "urn:carecomply:problem:authentication-required";
    public const string AuthenticationUnavailable = "urn:carecomply:problem:authentication-unavailable";
    public const string InternalServerError = "urn:carecomply:problem:internal-server-error";
    public const string MfaChallengeExpired = "urn:carecomply:problem:mfa-challenge-expired";
    public const string MfaEnrollmentState = "urn:carecomply:problem:mfa-enrollment-state";
    public const string MfaRateLimit = "urn:carecomply:problem:mfa-rate-limit";
    public const string MfaRecoveryFailed = "urn:carecomply:problem:mfa-recovery-failed";
    public const string MfaVerificationFailed = "urn:carecomply:problem:mfa-verification-failed";
    public const string RateLimit = "urn:carecomply:problem:authentication-rate-limit";
    public const string Validation = "urn:carecomply:problem:validation";
}
