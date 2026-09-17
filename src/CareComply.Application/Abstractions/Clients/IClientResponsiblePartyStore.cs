namespace CareComply.Application.Abstractions.Clients;

public sealed record ResponsiblePartyItem(Guid Id, string Name, string Relationship, string Phone,
    string Email, string Address, bool? LivesWithClient, bool? HasSpenddown, bool IsActive);

public sealed record SaveResponsibleParty(string Name, string Relationship, string Phone,
    string? Email, string? Address, bool LivesWithClient, bool HasSpenddown, bool IsActive);

public interface IClientResponsiblePartyStore
{
    Task<IReadOnlyList<ResponsiblePartyItem>?> ListAsync(Guid clientId, CancellationToken cancellationToken);
    Task<ResponsiblePartyItem?> CreateAsync(Guid clientId, SaveResponsibleParty request, CancellationToken cancellationToken);
    Task<ResponsiblePartyItem?> UpdateAsync(Guid clientId, Guid partyId, SaveResponsibleParty request, CancellationToken cancellationToken);
}
