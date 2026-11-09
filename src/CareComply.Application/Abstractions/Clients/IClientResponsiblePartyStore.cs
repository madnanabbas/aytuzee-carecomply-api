using CareComply.Domain.Clients;

namespace CareComply.Application.Abstractions.Clients;

public sealed record ResponsiblePartyItem(Guid Id, string Name, string Relationship, string Phone,
    string Email, string Address, bool? LivesWithClient, bool? HasSpenddown, bool IsActive,
    string? FirstName = null, string? MiddleName = null, string? LastName = null);

public sealed record SaveResponsibleParty(string Name, string Relationship, string Phone,
    string? Email, string? Address, bool LivesWithClient, bool HasSpenddown, bool IsActive,
    string? FirstName = null, string? MiddleName = null, string? LastName = null);

public interface IClientResponsiblePartyStore
{
    Task<IReadOnlyList<ResponsiblePartyItem>?> ListAsync(Guid clientId, CancellationToken cancellationToken);
    Task<ResponsiblePartyItem?> CreateAsync(Guid clientId, SaveResponsibleParty request, CancellationToken cancellationToken);
    Task<ResponsiblePartyItem?> UpdateAsync(Guid clientId, Guid partyId, SaveResponsibleParty request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ResponsiblePartyItem>> ListForClientAsync(Client client, CancellationToken cancellationToken);
    Task<ResponsiblePartyItem> CreateForClientAsync(Client client, SaveResponsibleParty request, CancellationToken cancellationToken);
    Task<ResponsiblePartyItem?> UpdateForClientAsync(Client client, Guid partyId, SaveResponsibleParty request, CancellationToken cancellationToken);
}
