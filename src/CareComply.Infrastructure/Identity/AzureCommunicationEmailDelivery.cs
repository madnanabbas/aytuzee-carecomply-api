using Azure;
using Azure.Communication.Email;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CareComply.Infrastructure.Identity;

internal sealed class AzureCommunicationEmailDelivery(
    IOptions<EmailDeliveryOptions> options,
    ILogger<AzureCommunicationEmailDelivery> logger)
{
    private static readonly Action<ILogger, Exception?> LogConfigurationFailure =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1500, "AuthenticationEmailConfigurationFailure"),
            "Authentication email could not be sent because provider configuration is unavailable.");

    private static readonly Action<ILogger, Exception?> LogProviderFailure =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1501, "AuthenticationEmailProviderFailure"),
            "Authentication email delivery failed at the configured provider.");

    public async Task<bool> SendAsync(
        string recipientAddress,
        EmailContent content,
        IReadOnlyCollection<EmailAttachment> attachments,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!Uri.TryCreate(
                settings.AzureCommunicationServicesEndpoint,
                UriKind.Absolute,
                out var endpoint)
            || endpoint.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(settings.SenderAddress))
        {
            LogConfigurationFailure(logger, null);
            return false;
        }

        try
        {
            var client = new EmailClient(endpoint, new DefaultAzureCredential());
            var message = new EmailMessage(
                settings.SenderAddress,
                new EmailRecipients([new EmailAddress(recipientAddress)]),
                content);
            foreach (var attachment in attachments)
            {
                message.Attachments.Add(attachment);
            }

            await client.SendAsync(WaitUntil.Started, message, cancellationToken);
            return true;
        }
        catch (RequestFailedException)
        {
            LogProviderFailure(logger, null);
        }
        catch (AuthenticationFailedException)
        {
            LogProviderFailure(logger, null);
        }
        catch (ArgumentException)
        {
            LogConfigurationFailure(logger, null);
        }

        return false;
    }
}
