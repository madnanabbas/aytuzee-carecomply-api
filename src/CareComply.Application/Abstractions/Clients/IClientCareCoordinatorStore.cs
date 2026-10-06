using CareComply.Domain.Clients;

namespace CareComply.Application.Abstractions.Clients;

public sealed record CareCoordinatorItem(Guid Id, string Name, string CompanyName, string Phone,
    string PhoneExtension, string Fax, string Email, bool IsActive);

public sealed record SaveCareCoordinator(string Name, string? CompanyName, string? Phone,
    string? PhoneExtension, string? Fax, string? Email, bool IsActive);

public interface IClientCareCoordinatorStore
{
    Task<IReadOnlyList<CareCoordinatorItem>?> ListAsync(Guid clientId, CancellationToken cancellationToken);
    Task<CareCoordinatorItem?> CreateAsync(Guid clientId, SaveCareCoordinator request, CancellationToken cancellationToken);
    Task<CareCoordinatorItem?> UpdateAsync(Guid clientId, Guid coordinatorId, SaveCareCoordinator request, CancellationToken cancellationToken);
    Task<IReadOnlyList<CareCoordinatorItem>> ListForClientAsync(Client client, CancellationToken cancellationToken);
    Task<CareCoordinatorItem> CreateForClientAsync(Client client, SaveCareCoordinator request, CancellationToken cancellationToken);
    Task<CareCoordinatorItem?> UpdateForClientAsync(Client client, Guid coordinatorId, SaveCareCoordinator request, CancellationToken cancellationToken);
}
