namespace CareComply.Application.Features.Authentication.Commands.Login;

public enum LoginStatus
{
    Succeeded,
    MfaRequired,
    MfaEnrollmentRequired,
    InvalidCredentials,
    Unavailable,
}
