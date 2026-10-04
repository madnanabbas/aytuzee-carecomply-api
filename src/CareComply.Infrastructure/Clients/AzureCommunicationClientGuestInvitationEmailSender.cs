using Azure.Communication.Email;
using CareComply.Application.Abstractions.Clients;
using CareComply.Infrastructure.Identity;

namespace CareComply.Infrastructure.Clients;

internal sealed class AzureCommunicationClientGuestInvitationEmailSender(
    AzureCommunicationEmailDelivery delivery) : IClientGuestInvitationEmailSender
{
    public Task<bool> SendAsync(
        ClientGuestInvitationEmailMessage message,
        CancellationToken cancellationToken)
    {
        return delivery.SendAsync(
            message.RecipientAddress,
            new EmailContent(message.Subject) { PlainText = message.PlainText, Html = message.Html },
            [],
            cancellationToken);
    }
}
