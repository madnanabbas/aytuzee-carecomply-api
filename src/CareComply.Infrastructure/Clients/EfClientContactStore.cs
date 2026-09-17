using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Clients;

internal sealed class EfClientContactStore(CareComplyDbContext db, ITenantScopeAccessor tenantAccessor)
    : IClientContactStore
{
    public async Task<IReadOnlyList<ClientContactItem>?> ListAsync(Guid clientId, CancellationToken cancellationToken)
    {
        if (!await CanAccessClient(clientId, cancellationToken)) return null;
        var contacts = await db.ClientContacts.AsNoTracking()
            .Where(contact => contact.ClientId == clientId && (contact.Type == ClientContactType.General || contact.Type == ClientContactType.Emergency))
            .OrderBy(contact => contact.Name).ToArrayAsync(cancellationToken);
        return contacts.Select(Map).ToArray();
    }

    public async Task<ClientContactItem?> CreateAsync(Guid clientId, SaveClientContact request, CancellationToken cancellationToken)
    {
        if (!await CanAccessClient(clientId, cancellationToken)) return null;
        var contact = new ClientContact(Guid.CreateVersion7(), clientId, request.Type, request.Name,
            request.Relationship, request.Phone, request.Email, false, request.Type == ClientContactType.Emergency,
            null, null, null);
        contact.UpdateContactDetails(request.Name, request.Relationship, request.Phone, request.Email,
            request.Address, request.IsActive);
        db.ClientContacts.Add(contact);
        await db.SaveChangesAsync(cancellationToken);
        return Map(contact);
    }

    public async Task<ClientContactItem?> UpdateAsync(Guid clientId, Guid contactId, SaveClientContact request, CancellationToken cancellationToken)
    {
        if (!await CanAccessClient(clientId, cancellationToken)) return null;
        var contact = await db.ClientContacts.SingleOrDefaultAsync(row => row.ClientId == clientId && row.Id == contactId
            && (row.Type == ClientContactType.General || row.Type == ClientContactType.Emergency), cancellationToken);
        if (contact is null) return null;
        // Contact type is immutable: changing it requires a new record to preserve emergency-contact history.
        if (contact.Type != request.Type) return null;
        contact.UpdateContactDetails(request.Name, request.Relationship, request.Phone, request.Email,
            request.Address, request.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return Map(contact);
    }

    private async Task<bool> CanAccessClient(Guid clientId, CancellationToken cancellationToken)
    {
        var tenant = await tenantAccessor.GetAsync(cancellationToken);
        return tenant is not null && await db.Clients.AsNoTracking().AnyAsync(client => client.Id == clientId
            && (tenant.IsPlatformAdministrator || (client.CompanyId == tenant.CompanyId
                && db.UserLocations.Any(access => access.UserId == tenant.UserId && access.LocationId == client.LocationId))), cancellationToken);
    }

    private static ClientContactItem Map(ClientContact contact) => new(contact.Id, contact.Type,
        contact.Name, contact.Relationship, contact.Phone, contact.Email, contact.Address, contact.IsActive);
}
