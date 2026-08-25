namespace CareComply.Application.Features.Authentication.Commands.ForgotPassword;

public sealed record ForgotPasswordResult
{
    public static ForgotPasswordResult Accepted { get; } = new();
}
