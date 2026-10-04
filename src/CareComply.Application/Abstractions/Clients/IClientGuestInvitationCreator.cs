namespace CareComply.Application.Abstractions.Clients;

public interface IClientGuestInvitationCreator
{
    Task<ClientGuestInvitationCreationResult> CreateAsync(
        ClientGuestInvitationCreationRequest request,
        CancellationToken cancellationToken);
}

public sealed record ClientGuestInvitationCreationRequest(
    string ClientName,
    string Email,
    string? MobileNumber,
    Guid? CompanyId);

public sealed record ClientGuestInvitationCreationResult(
    ClientGuestInvitationCreationStatus Status,
    Guid? InvitationId = null,
    string? GuestLink = null,
    DateTimeOffset? ExpiresAtUtc = null);

public enum ClientGuestInvitationCreationStatus
{
    Succeeded,
    NotFound,
    InvalidConfiguration,
}
