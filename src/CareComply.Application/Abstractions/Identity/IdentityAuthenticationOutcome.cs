namespace CareComply.Application.Abstractions.Identity;

public enum IdentityAuthenticationOutcome
{
    Succeeded,
    RequiresTwoFactor,
    RequiresMfaEnrollment,
    Failed,
    Unavailable,
}
