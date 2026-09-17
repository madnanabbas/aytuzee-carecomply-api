using CareComply.Domain.Clients;

namespace CareComply.Application.Abstractions.Clients;

public sealed record ClinicalContactItem(Guid Id, string Name, string Phone, string Email,
    string SecondaryName, string SecondaryPhone, string SecondaryAddress,
    string Npi, string Fax, string Address, string PostalCode, string City, string State, bool IsActive);

public sealed record SaveConsultationProvider(string Name, string? Phone, string? Email,
    string? SupportProviderName, string? SupportProviderPhone, string? SupportProviderAddress, bool IsActive);

public sealed record SavePhysician(string Name, string? Npi, string? Phone, string? Fax,
    string? Address, string? PostalCode, string? City, string? State, bool IsActive);

public interface IClientClinicalContactStore
{
    Task<IReadOnlyList<ClinicalContactItem>?> ListAsync(Guid clientId, ClientContactType type, CancellationToken cancellationToken);
    Task<ClinicalContactItem?> CreateConsultationProviderAsync(Guid clientId, SaveConsultationProvider request, CancellationToken cancellationToken);
    Task<ClinicalContactItem?> UpdateConsultationProviderAsync(Guid clientId, Guid contactId, SaveConsultationProvider request, CancellationToken cancellationToken);
    Task<ClinicalContactItem?> CreatePhysicianAsync(Guid clientId, SavePhysician request, CancellationToken cancellationToken);
    Task<ClinicalContactItem?> UpdatePhysicianAsync(Guid clientId, Guid contactId, SavePhysician request, CancellationToken cancellationToken);
}
