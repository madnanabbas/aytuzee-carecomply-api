using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Clients;

internal sealed class EfClientClinicalContactStore(CareComplyDbContext db, ITenantScopeAccessor tenantAccessor)
    : IClientClinicalContactStore
{
    public async Task<IReadOnlyList<ClinicalContactItem>?> ListAsync(Guid clientId, ClientContactType type, CancellationToken cancellationToken)
    {
        if (type is not (ClientContactType.ConsultationProvider or ClientContactType.Physician))
            throw new ArgumentOutOfRangeException(nameof(type));
        if (!await CanAccessClient(clientId, cancellationToken)) return null;
        var contacts = await db.ClientContacts.AsNoTracking().Where(x => x.ClientId == clientId && x.Type == type)
            .OrderBy(x => x.Name).ToArrayAsync(cancellationToken);
        return contacts.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<ClinicalContactItem>> ListForClientAsync(
        Client client, ClientContactType type, CancellationToken cancellationToken)
    {
        if (type is not (ClientContactType.ConsultationProvider or ClientContactType.Physician))
            throw new ArgumentOutOfRangeException(nameof(type));
        var contacts = await db.ClientContacts.AsNoTracking()
            .Where(x => x.ClientId == client.Id && x.Type == type)
            .OrderBy(x => x.Name).ToArrayAsync(cancellationToken);
        return contacts.Select(Map).ToArray();
    }

    public async Task<ClinicalContactItem?> CreateConsultationProviderAsync(Guid clientId, SaveConsultationProvider request, CancellationToken cancellationToken)
    {
        if (!await CanAccessClient(clientId, cancellationToken)) return null;
        return await PersistConsultationProviderAsync(clientId, request, cancellationToken);
    }

    public async Task<ClinicalContactItem> CreateConsultationProviderForClientAsync(
        Client client, SaveConsultationProvider request, CancellationToken cancellationToken)
        => await PersistConsultationProviderAsync(client.Id, request, cancellationToken);

    private async Task<ClinicalContactItem> PersistConsultationProviderAsync(
        Guid clientId, SaveConsultationProvider request, CancellationToken cancellationToken)
    {
        var contact = NewContact(clientId, ClientContactType.ConsultationProvider, request.Name);
        contact.UpdateConsultationProvider(request.Name, request.Phone, request.Email,
            request.SupportProviderName, request.SupportProviderPhone, request.SupportProviderAddress, request.IsActive);
        db.ClientContacts.Add(contact);
        await db.SaveChangesAsync(cancellationToken);
        return Map(contact);
    }

    public async Task<ClinicalContactItem?> UpdateConsultationProviderAsync(Guid clientId, Guid contactId, SaveConsultationProvider request, CancellationToken cancellationToken)
    {
        var contact = await FindAccessibleContact(clientId, contactId, ClientContactType.ConsultationProvider, cancellationToken);
        if (contact is null) return null;
        contact.UpdateConsultationProvider(request.Name, request.Phone, request.Email,
            request.SupportProviderName, request.SupportProviderPhone, request.SupportProviderAddress, request.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return Map(contact);
    }

    public async Task<ClinicalContactItem?> UpdateConsultationProviderForClientAsync(
        Client client, Guid contactId, SaveConsultationProvider request, CancellationToken cancellationToken)
    {
        var contact = await db.ClientContacts.SingleOrDefaultAsync(
            x => x.ClientId == client.Id && x.Id == contactId
                && x.Type == ClientContactType.ConsultationProvider, cancellationToken);
        if (contact is null) return null;
        contact.UpdateConsultationProvider(request.Name, request.Phone, request.Email,
            request.SupportProviderName, request.SupportProviderPhone,
            request.SupportProviderAddress, request.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return Map(contact);
    }

    public async Task<ClinicalContactItem?> CreatePhysicianAsync(Guid clientId, SavePhysician request, CancellationToken cancellationToken)
    {
        if (!await CanAccessClient(clientId, cancellationToken)) return null;
        return await PersistPhysicianAsync(clientId, request, cancellationToken);
    }

    public async Task<ClinicalContactItem> CreatePhysicianForClientAsync(
        Client client, SavePhysician request, CancellationToken cancellationToken) =>
        await PersistPhysicianAsync(client.Id, request, cancellationToken);

    private async Task<ClinicalContactItem> PersistPhysicianAsync(
        Guid clientId, SavePhysician request, CancellationToken cancellationToken)
    {
        var contact = NewContact(clientId, ClientContactType.Physician, request.Name);
        contact.UpdatePhysician(request.Name, request.Npi, request.Phone, request.Fax,
            request.Address, request.PostalCode, request.City, request.State, request.IsActive);
        db.ClientContacts.Add(contact);
        await db.SaveChangesAsync(cancellationToken);
        return Map(contact);
    }

    public async Task<ClinicalContactItem?> UpdatePhysicianAsync(Guid clientId, Guid contactId, SavePhysician request, CancellationToken cancellationToken)
    {
        var contact = await FindAccessibleContact(clientId, contactId, ClientContactType.Physician, cancellationToken);
        if (contact is null) return null;
        contact.UpdatePhysician(request.Name, request.Npi, request.Phone, request.Fax,
            request.Address, request.PostalCode, request.City, request.State, request.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return Map(contact);
    }

    public async Task<ClinicalContactItem?> UpdatePhysicianForClientAsync(
        Client client, Guid contactId, SavePhysician request, CancellationToken cancellationToken)
    {
        var contact = await db.ClientContacts.SingleOrDefaultAsync(
            x => x.ClientId == client.Id && x.Id == contactId
                && x.Type == ClientContactType.Physician, cancellationToken);
        if (contact is null) return null;
        contact.UpdatePhysician(request.Name, request.Npi, request.Phone, request.Fax,
            request.Address, request.PostalCode, request.City, request.State, request.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return Map(contact);
    }

    private async Task<ClientContact?> FindAccessibleContact(Guid clientId, Guid id, ClientContactType type, CancellationToken cancellationToken)
    {
        if (!await CanAccessClient(clientId, cancellationToken)) return null;
        return await db.ClientContacts.SingleOrDefaultAsync(x => x.ClientId == clientId && x.Id == id && x.Type == type, cancellationToken);
    }

    private async Task<bool> CanAccessClient(Guid clientId, CancellationToken cancellationToken)
    {
        var tenant = await tenantAccessor.GetAsync(cancellationToken);
        return tenant is not null && await db.Clients.AsNoTracking().AnyAsync(client => client.Id == clientId
            && (tenant.IsPlatformAdministrator || (client.CompanyId == tenant.CompanyId
                && db.UserLocations.Any(access => access.UserId == tenant.UserId && access.LocationId == client.LocationId))), cancellationToken);
    }

    private static ClientContact NewContact(Guid clientId, ClientContactType type, string name) =>
        new(Guid.CreateVersion7(), clientId, type, name, null, null, null, false, false, null, null, null);

    private static ClinicalContactItem Map(ClientContact x) => new(x.Id, x.Name, x.Phone, x.Email,
        x.SecondaryName, x.SecondaryPhone, x.SecondaryAddress, x.Npi, x.Fax,
        x.Address, x.PostalCode, x.City, x.State, x.IsActive);
}
