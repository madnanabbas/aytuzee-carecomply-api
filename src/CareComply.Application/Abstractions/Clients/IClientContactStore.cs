using CareComply.Domain.Clients;

namespace CareComply.Application.Abstractions.Clients;

public sealed record ClientContactItem(Guid Id, ClientContactType Type, string Name, string Relationship,
    string Phone, string Email, string Address, bool IsActive);

public sealed record SaveClientContact(ClientContactType Type, string Name, string Relationship,
    string Phone, string? Email, string? Address, bool IsActive);

public interface IClientContactStore
{
    Task<IReadOnlyList<ClientContactItem>?> ListAsync(Guid clientId, CancellationToken cancellationToken);
    Task<ClientContactItem?> CreateAsync(Guid clientId, SaveClientContact request, CancellationToken cancellationToken);
    Task<ClientContactItem?> UpdateAsync(Guid clientId, Guid contactId, SaveClientContact request, CancellationToken cancellationToken);
}
