namespace CareComply.Application.Abstractions.Clients;

public interface IClientGuestSessionService
{
    Task<ClientGuestRedemptionResult> RedeemAsync(string invitationToken, CancellationToken cancellationToken);
    Task<ClientGuestSessionStateResult> GetStateAsync(string sessionToken, CancellationToken cancellationToken);
    Task<bool> ValidateCsrfAsync(
        string sessionToken,
        string csrfToken,
        CancellationToken cancellationToken);
    Task InvalidateAsync(string sessionToken, DateTimeOffset completedAtUtc, CancellationToken cancellationToken);
}

public enum ClientGuestAccessState
{
    Ready,
    Used,
    Invalid,
    Expired,
    Unavailable,
    Completed,
    SessionExpired,
}

public sealed record ClientGuestRedemptionResult(
    ClientGuestAccessState State,
    string? SessionToken = null,
    string? CsrfToken = null,
    string? ClientDisplayName = null,
    DateTimeOffset? InvitationExpiresAtUtc = null,
    bool HasCreatedClient = false,
    int CreationProgressStep = 1);

public sealed record ClientGuestSessionStateResult(
    ClientGuestAccessState State,
    string? ClientDisplayName = null,
    DateTimeOffset? InvitationExpiresAtUtc = null,
    bool HasCreatedClient = false,
    int CreationProgressStep = 1);
