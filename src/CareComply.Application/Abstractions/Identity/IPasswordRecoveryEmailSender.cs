namespace CareComply.Application.Abstractions.Identity;

public interface IPasswordRecoveryEmailSender
{
    Task SendAsync(
        string recipientAddress,
        string resetUrl,
        CancellationToken cancellationToken);
}
