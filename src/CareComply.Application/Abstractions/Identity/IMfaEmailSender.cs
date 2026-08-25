namespace CareComply.Application.Abstractions.Identity;

public interface IMfaEmailSender
{
    Task<bool> SendCodeAsync(
        string recipientAddress,
        string code,
        CancellationToken cancellationToken);
}
