using Azure.Communication.Email;
using CareComply.Application.Abstractions.Identity;

namespace CareComply.Infrastructure.Identity;

internal sealed class AzureCommunicationPasswordRecoveryEmailSender(
    AzureCommunicationEmailDelivery emailDelivery)
    : IPasswordRecoveryEmailSender
{
    public async Task SendAsync(
        string recipientAddress,
        string resetUrl,
        CancellationToken cancellationToken)
    {
        var template = PasswordRecoveryEmailTemplate.Create(resetUrl);
        var content = new EmailContent(template.Subject)
        {
            PlainText = template.PlainText,
            Html = template.Html,
        };
        _ = await emailDelivery.SendAsync(
            recipientAddress,
            content,
            [PasswordRecoveryEmailBranding.CreateLogoAttachment()],
            cancellationToken);
    }
}
