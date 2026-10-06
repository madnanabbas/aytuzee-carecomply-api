namespace CareComply.Application.Abstractions.Clients;

public interface IClientGuestInvitationSender
{
    Task<ClientGuestInvitationSendResult> SendAsync(
        Guid invitationId,
        string token,
        CancellationToken cancellationToken);
}

public interface IClientGuestInvitationEmailSender
{
    Task<bool> SendAsync(
        ClientGuestInvitationEmailMessage message,
        CancellationToken cancellationToken);
}

public sealed record ClientGuestInvitationEmailMessage(
    string RecipientAddress,
    string Subject,
    string PlainText,
    string Html)
{
    public override string ToString() =>
        $"ClientGuestInvitationEmailMessage {{ RecipientAddress = [REDACTED], Subject = {Subject}, PlainText = [REDACTED], Html = [REDACTED] }}";
}

public sealed record ClientGuestInvitationSendResult(
    ClientGuestInvitationSendStatus Status,
    Guid InvitationId,
    DateTimeOffset? SentAtUtc = null,
    DateTimeOffset? ExpiresAtUtc = null);

public enum ClientGuestInvitationSendStatus
{
    Succeeded,
    NotFound,
    InvalidToken,
    Unavailable,
    DeliveryFailed,
    Conflict,
    InvalidConfiguration,
}
