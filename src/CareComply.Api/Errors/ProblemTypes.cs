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
    public const string NavigationConflict = "urn:carecomply:problem:navigation-conflict";
    public const string NavigationDuplicateKey = "urn:carecomply:problem:navigation-duplicate-key";
    public const string NavigationDuplicateRoute = "urn:carecomply:problem:navigation-duplicate-route";
    public const string NavigationInvalidParent = "urn:carecomply:problem:navigation-invalid-parent";
    public const string NavigationNotFound = "urn:carecomply:problem:navigation-not-found";
    public const string PasswordResetInvalid = "urn:carecomply:problem:password-reset-invalid";
    public const string PasswordRecoveryRateLimit = "urn:carecomply:problem:password-recovery-rate-limit";
    public const string RoleConflict = "urn:carecomply:problem:role-conflict";
    public const string RoleDuplicateName = "urn:carecomply:problem:role-duplicate-name";
    public const string RoleLastManager = "urn:carecomply:problem:role-last-manager";
    public const string RoleNotFound = "urn:carecomply:problem:role-not-found";
    public const string RolePermissionInvalid = "urn:carecomply:problem:role-permission-invalid";
    public const string RateLimit = "urn:carecomply:problem:authentication-rate-limit";
    public const string Validation = "urn:carecomply:problem:validation";
    public const string UserConflict = "urn:carecomply:problem:user-conflict";
    public const string UserDuplicateEmail = "urn:carecomply:problem:user-duplicate-email";
    public const string UserNotFound = "urn:carecomply:problem:user-not-found";
    public const string UserPasswordInvalid = "urn:carecomply:problem:user-password-invalid";
    public const string UserProvisioningFailed = "urn:carecomply:problem:user-provisioning-failed";
    public const string UserRoleInvalid = "urn:carecomply:problem:user-role-invalid";
    public const string UserSelfDeactivation = "urn:carecomply:problem:user-self-deactivation";
    public const string UserMfaRequiredByPermissions = "urn:carecomply:problem:user-mfa-required-by-permissions";
    public const string UserMfaMethodInvalid = "urn:carecomply:problem:user-mfa-method-invalid";
}
