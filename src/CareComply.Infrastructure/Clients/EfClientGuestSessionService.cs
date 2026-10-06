using System.Security.Cryptography;
using CareComply.Application.Abstractions.Clients;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareComply.Infrastructure.Clients;

internal sealed class EfClientGuestSessionService(
    CareComplyDbContext context,
    TimeProvider timeProvider,
    IOptions<ClientGuestInvitationOptions> options) : IClientGuestSessionService
{
    public async Task<ClientGuestRedemptionResult> RedeemAsync(
        string invitationToken,
        CancellationToken cancellationToken)
    {
        if (!ClientGuestInvitationToken.TryHash(invitationToken, out var invitationTokenHash))
            return new(ClientGuestAccessState.Invalid);

        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<ClientGuestRedemptionResult>(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var invitation = await context.ClientGuestInvitations.SingleOrDefaultAsync(
                value => value.TokenHash.SequenceEqual(invitationTokenHash), cancellationToken);
            if (invitation is null) return new(ClientGuestAccessState.Invalid);
            var lifecycle = InvitationState(invitation);
            if (lifecycle != ClientGuestAccessState.Ready) return new(lifecycle);
            if (invitation.RedeemedAtUtc is not null) return new(ClientGuestAccessState.Used);

            var now = UtcNow();
            var sessionToken = CreateToken();
            var csrfToken = CreateToken();
            var absoluteExpiry = now + options.Value.GuestSessionAbsoluteLifetime;
            var expiresAtUtc = absoluteExpiry < invitation.ExpiresAtUtc
                ? absoluteExpiry
                : invitation.ExpiresAtUtc;
            invitation.Redeem(now);
            context.ClientGuestSessions.Add(ClientGuestSession.Create(
                Guid.NewGuid(),
                invitation.Id,
                SHA256.HashData(Convert.FromBase64String(ToBase64(sessionToken))),
                SHA256.HashData(Convert.FromBase64String(ToBase64(csrfToken))),
                now,
                expiresAtUtc));
            try
            {
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                var progress = await ProgressAsync(invitation.CreatedClientId, cancellationToken);
                return new(
                    ClientGuestAccessState.Ready,
                    sessionToken,
                    csrfToken,
                    invitation.ClientName,
                    invitation.ExpiresAtUtc,
                    invitation.CreatedClientId is not null,
                    progress);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(ClientGuestAccessState.Used);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(ClientGuestAccessState.Used);
            }
        });
    }

    public async Task<ClientGuestSessionStateResult> GetStateAsync(
        string sessionToken,
        CancellationToken cancellationToken)
    {
        var resolution = await ResolveAsync(sessionToken, true, cancellationToken);
        if (resolution.Session is null || resolution.Invitation is null)
            return new(resolution.State);
        return new(
            ClientGuestAccessState.Ready,
            resolution.Invitation.ClientName,
            resolution.Invitation.ExpiresAtUtc,
            resolution.Invitation.CreatedClientId is not null,
            await ProgressAsync(resolution.Invitation.CreatedClientId, cancellationToken));
    }

    public async Task<bool> ValidateCsrfAsync(
        string sessionToken,
        string csrfToken,
        CancellationToken cancellationToken)
    {
        if (!TryHash(sessionToken, out var sessionHash)
            || !TryHash(csrfToken, out var csrfHash))
            return false;

        // CSRF validation establishes that both values belong to the same issued
        // session. Lifecycle validation remains in the requested operation so it
        // can return the established Expired/Unavailable/Completed response.
        var session = await context.ClientGuestSessions.AsNoTracking().SingleOrDefaultAsync(
            value => value.SessionTokenHash.SequenceEqual(sessionHash), cancellationToken);
        return session is not null
            && CryptographicOperations.FixedTimeEquals(session.CsrfTokenHash, csrfHash);
    }

    public async Task InvalidateAsync(
        string sessionToken,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        if (!TryHash(sessionToken, out var hash)) return;
        var session = await context.ClientGuestSessions.AsNoTracking().SingleOrDefaultAsync(
            value => value.SessionTokenHash.SequenceEqual(hash), cancellationToken);
        if (session is null || session.CompletedAtUtc is not null) return;
        session.Complete(completedAtUtc);
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<(ClientGuestAccessState State, ClientGuestSession? Session, ClientGuestInvitation? Invitation)>
        ResolveAsync(string rawToken, bool touch, CancellationToken cancellationToken)
    {
        if (!TryHash(rawToken, out var hash)) return (ClientGuestAccessState.Invalid, null, null);
        var session = await context.ClientGuestSessions.SingleOrDefaultAsync(
            value => value.SessionTokenHash.SequenceEqual(hash), cancellationToken);
        if (session is null) return (ClientGuestAccessState.Invalid, null, null);
        var invitation = await context.ClientGuestInvitations.AsNoTracking().SingleAsync(
            value => value.Id == session.ClientGuestInvitationId, cancellationToken);
        var invitationState = InvitationState(invitation);
        if (invitationState != ClientGuestAccessState.Ready)
            return (invitationState, null, invitation);
        var now = UtcNow();
        if (session.RevokedAtUtc is not null || session.CompletedAtUtc is not null
            || now >= session.ExpiresAtUtc
            || now - session.LastActivityAtUtc >= options.Value.GuestSessionInactivityTimeout)
            return (ClientGuestAccessState.SessionExpired, null, invitation);
        if (touch)
        {
            await context.ClientGuestSessions
                .Where(value => value.Id == session.Id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(value => value.LastActivityAtUtc, now),
                    cancellationToken);
        }
        return (ClientGuestAccessState.Ready, session, invitation);
    }

    private ClientGuestAccessState InvitationState(ClientGuestInvitation invitation)
    {
        if (invitation.CompletedAtUtc is not null) return ClientGuestAccessState.Completed;
        if (invitation.RevokedAtUtc is not null) return ClientGuestAccessState.Unavailable;
        return UtcNow() >= invitation.ExpiresAtUtc
            ? ClientGuestAccessState.Expired
            : ClientGuestAccessState.Ready;
    }

    private async Task<int> ProgressAsync(Guid? clientId, CancellationToken cancellationToken) =>
        clientId is null
            ? 1
            : await context.Clients.AsNoTracking()
                .Where(value => value.Id == clientId)
                .Select(value => value.CreationProgressStep)
                .SingleOrDefaultAsync(cancellationToken);

    private DateTimeOffset UtcNow()
    {
        var value = timeProvider.GetUtcNow();
        return new DateTimeOffset(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
    }

    private static string CreateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryHash(string token, out byte[] hash)
    {
        hash = [];
        try
        {
            hash = SHA256.HashData(Convert.FromBase64String(ToBase64(token)));
            return token.Length == 43;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string ToBase64(string token) => token.Replace('-', '+').Replace('_', '/') + "=";
}
