using System.Security.Cryptography;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareComply.Infrastructure.Clients;

internal sealed class EfClientGuestInvitationSender(
    CareComplyDbContext context,
    ITenantScopeAccessor tenantAccessor,
    IClientGuestInvitationEmailSender emailSender,
    IOptions<ClientGuestInvitationOptions> options,
    TimeProvider timeProvider) : IClientGuestInvitationSender
{
    public async Task<ClientGuestInvitationSendResult> SendAsync(
        Guid invitationId,
        string token,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantAccessor.GetAsync(cancellationToken);
        if (tenant is null) return Result(ClientGuestInvitationSendStatus.NotFound, invitationId);
        var invitation = await context.ClientGuestInvitations.SingleOrDefaultAsync(
            item => item.Id == invitationId
                && (tenant.IsPlatformAdministrator || item.CompanyId == tenant.CompanyId),
            cancellationToken);
        if (invitation is null) return Result(ClientGuestInvitationSendStatus.NotFound, invitationId);

        var now = timeProvider.GetUtcNow();
        if (!invitation.CanSend(now))
            return Result(ClientGuestInvitationSendStatus.Unavailable, invitationId, invitation.ExpiresAtUtc);
        if (!ClientGuestInvitationToken.TryHash(token, out var suppliedHash)
            || !CryptographicOperations.FixedTimeEquals(suppliedHash, invitation.TokenHash))
            return Result(ClientGuestInvitationSendStatus.InvalidToken, invitationId, invitation.ExpiresAtUtc);
        if (!options.Value.TryBuildGuestLink(token, out var guestLink))
            return Result(ClientGuestInvitationSendStatus.InvalidConfiguration, invitationId, invitation.ExpiresAtUtc);

        var content = ClientGuestInvitationEmailTemplate.Create(
            invitation.ClientName,
            guestLink,
            invitation.ExpiresAtUtc);
        var message = new ClientGuestInvitationEmailMessage(
            invitation.Email,
            content.Subject,
            content.PlainText,
            content.Html);
        if (!await emailSender.SendAsync(message, cancellationToken))
            return Result(ClientGuestInvitationSendStatus.DeliveryFailed, invitationId, invitation.ExpiresAtUtc);

        invitation.MarkSent(now);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result(ClientGuestInvitationSendStatus.Conflict, invitationId, invitation.ExpiresAtUtc);
        }
        return new(
            ClientGuestInvitationSendStatus.Succeeded,
            invitation.Id,
            invitation.SentAtUtc,
            invitation.ExpiresAtUtc);
    }

    private static ClientGuestInvitationSendResult Result(
        ClientGuestInvitationSendStatus status,
        Guid invitationId,
        DateTimeOffset? expiresAtUtc = null) =>
        new(status, invitationId, ExpiresAtUtc: expiresAtUtc);
}
