namespace CareComply.Application.Features.Authentication.Commands.ResetPassword;

public sealed record ResetPasswordResult(ResetPasswordStatus Status);

public enum ResetPasswordStatus
{
    Succeeded,
    Invalid,
}
