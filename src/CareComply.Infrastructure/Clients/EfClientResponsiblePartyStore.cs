using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Clients;

internal sealed class EfClientResponsiblePartyStore(CareComplyDbContext db, ITenantScopeAccessor tenantAccessor)
    : IClientResponsiblePartyStore
{
    public async Task<IReadOnlyList<ResponsiblePartyItem>?> ListAsync(Guid clientId, CancellationToken cancellationToken)
    {
        if (!await CanAccessClient(clientId, cancellationToken)) return null;
        var parties = await db.ClientContacts.AsNoTracking()
            .Where(contact => contact.ClientId == clientId && contact.Type == ClientContactType.ResponsibleParty)
            .OrderBy(contact => contact.Name).ToArrayAsync(cancellationToken);
        return parties.Select(Map).ToArray();
    }

    public async Task<ResponsiblePartyItem?> CreateAsync(Guid clientId, SaveResponsibleParty request, CancellationToken cancellationToken)
    {
        if (!await CanAccessClient(clientId, cancellationToken)) return null;
        var party = new ClientContact(Guid.CreateVersion7(), clientId, ClientContactType.ResponsibleParty,
            request.Name, request.Relationship, request.Phone, request.Email, false, false, null, null, null);
        party.UpdateResponsibleParty(request.Name, request.Relationship, request.Phone, request.Email,
            request.Address, request.LivesWithClient, request.HasSpenddown, request.IsActive);
        db.ClientContacts.Add(party);
        await db.SaveChangesAsync(cancellationToken);
        return Map(party);
    }

    public async Task<ResponsiblePartyItem?> UpdateAsync(Guid clientId, Guid partyId, SaveResponsibleParty request, CancellationToken cancellationToken)
    {
        if (!await CanAccessClient(clientId, cancellationToken)) return null;
        var party = await db.ClientContacts.SingleOrDefaultAsync(contact => contact.Id == partyId
            && contact.ClientId == clientId && contact.Type == ClientContactType.ResponsibleParty, cancellationToken);
        if (party is null) return null;
        party.UpdateResponsibleParty(request.Name, request.Relationship, request.Phone, request.Email,
            request.Address, request.LivesWithClient, request.HasSpenddown, request.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return Map(party);
    }

    private async Task<bool> CanAccessClient(Guid clientId, CancellationToken cancellationToken)
    {
        var tenant = await tenantAccessor.GetAsync(cancellationToken);
        return tenant is not null && await db.Clients.AsNoTracking().AnyAsync(client => client.Id == clientId
            && (tenant.IsPlatformAdministrator || (client.CompanyId == tenant.CompanyId
                && db.UserLocations.Any(access => access.UserId == tenant.UserId && access.LocationId == client.LocationId))), cancellationToken);
    }

    private static ResponsiblePartyItem Map(ClientContact party) => new(party.Id, party.Name, party.Relationship,
        party.Phone, party.Email, party.Address, party.LivesWithClient, party.HasSpenddown, party.IsActive);
}
