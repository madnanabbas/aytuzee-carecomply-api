using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Clients;

internal sealed class EfClientCareCoordinatorStore(CareComplyDbContext db, ITenantScopeAccessor tenantAccessor)
    : IClientCareCoordinatorStore
{
    public async Task<IReadOnlyList<CareCoordinatorItem>?> ListAsync(Guid clientId, CancellationToken cancellationToken)
    {
        if (!await CanAccessClient(clientId, cancellationToken)) return null;
        var coordinators = await db.ClientContacts.AsNoTracking()
            .Where(contact => contact.ClientId == clientId && contact.Type == ClientContactType.CareCoordinator)
            .OrderBy(contact => contact.Name).ToArrayAsync(cancellationToken);
        return coordinators.Select(Map).ToArray();
    }

    public async Task<CareCoordinatorItem?> CreateAsync(Guid clientId, SaveCareCoordinator request, CancellationToken cancellationToken)
    {
        if (!await CanAccessClient(clientId, cancellationToken)) return null;
        var coordinator = new ClientContact(Guid.CreateVersion7(), clientId, ClientContactType.CareCoordinator,
            request.Name, null, request.Phone, request.Email, false, false, null, null, null);
        coordinator.UpdateCareCoordinator(request.Name, request.CompanyName, request.Phone,
            request.PhoneExtension, request.Fax, request.Email, request.IsActive);
        db.ClientContacts.Add(coordinator);
        await db.SaveChangesAsync(cancellationToken);
        return Map(coordinator);
    }

    public async Task<CareCoordinatorItem?> UpdateAsync(Guid clientId, Guid coordinatorId, SaveCareCoordinator request, CancellationToken cancellationToken)
    {
        if (!await CanAccessClient(clientId, cancellationToken)) return null;
        var coordinator = await db.ClientContacts.SingleOrDefaultAsync(contact => contact.ClientId == clientId
            && contact.Id == coordinatorId && contact.Type == ClientContactType.CareCoordinator, cancellationToken);
        if (coordinator is null) return null;
        coordinator.UpdateCareCoordinator(request.Name, request.CompanyName, request.Phone,
            request.PhoneExtension, request.Fax, request.Email, request.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return Map(coordinator);
    }

    private async Task<bool> CanAccessClient(Guid clientId, CancellationToken cancellationToken)
    {
        var tenant = await tenantAccessor.GetAsync(cancellationToken);
        return tenant is not null && await db.Clients.AsNoTracking().AnyAsync(client => client.Id == clientId
            && (tenant.IsPlatformAdministrator || (client.CompanyId == tenant.CompanyId
                && db.UserLocations.Any(access => access.UserId == tenant.UserId && access.LocationId == client.LocationId))), cancellationToken);
    }

    private static CareCoordinatorItem Map(ClientContact coordinator) => new(coordinator.Id,
        coordinator.Name, coordinator.CompanyName, coordinator.Phone, coordinator.PhoneExtension,
        coordinator.Fax, coordinator.Email, coordinator.IsActive);
}
