using System.Security.Cryptography;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Clients;

internal sealed class EfClientHospitalizationStore(
    CareComplyDbContext context,
    ITenantScopeAccessor tenantScopeAccessor) : IClientHospitalizationStore
{
    public async Task<HospitalizationList?> List(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessClientAsync(clientId, cancellationToken))
        {
            return null;
        }

        var hospitalizations = await context.ClientHospitalizations
            .AsNoTracking()
            .Where(hospitalization => hospitalization.ClientId == clientId)
            .OrderByDescending(hospitalization => hospitalization.AdmittedAtUtc)
            .ToArrayAsync(cancellationToken);

        return new(hospitalizations.Select(Map).ToArray());
    }

    public async Task<HospitalizationList> ListForClient(
        Client client, CancellationToken cancellationToken)
    {
        var hospitalizations = await context.ClientHospitalizations.AsNoTracking()
            .Where(value => value.ClientId == client.Id)
            .OrderByDescending(value => value.AdmittedAtUtc).ToArrayAsync(cancellationToken);
        return new(hospitalizations.Select(Map).ToArray());
    }

    public async Task<HospitalizationMutation> Create(
        HospitalizationCreate request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessClientAsync(request.ClientId, cancellationToken))
        {
            return new(HospitalizationMutationStatus.ClientNotFound);
        }

        return await PersistCreate(request, cancellationToken);
    }

    public Task<HospitalizationMutation> CreateForClient(
        Client client, HospitalizationCreate request, CancellationToken cancellationToken) =>
        PersistCreate(request with { ClientId = client.Id }, cancellationToken);

    private async Task<HospitalizationMutation> PersistCreate(
        HospitalizationCreate request, CancellationToken cancellationToken)
    {
        var hospitalization = ClientHospitalization.Create(
            Guid.CreateVersion7(),
            request.ClientId,
            request.HospitalName,
            request.AdmittedAtUtc,
            request.DischargedAtUtc,
            request.Reason,
            request.Diagnosis,
            request.DischargeInstructions,
            request.ActorId,
            request.NowUtc,
            request.GuestInvitationId);

        context.ClientHospitalizations.Add(hospitalization);
        await context.SaveChangesAsync(cancellationToken);

        return new(HospitalizationMutationStatus.Succeeded, Map(hospitalization));
    }

    public async Task<HospitalizationMutation> Update(
        HospitalizationUpdate request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessClientAsync(request.ClientId, cancellationToken))
        {
            return new(HospitalizationMutationStatus.ClientNotFound);
        }

        return await PersistUpdate(request, cancellationToken);
    }

    public Task<HospitalizationMutation> UpdateForClient(
        Client client, HospitalizationUpdate request, CancellationToken cancellationToken) =>
        PersistUpdate(request with { ClientId = client.Id }, cancellationToken);

    private async Task<HospitalizationMutation> PersistUpdate(
        HospitalizationUpdate request, CancellationToken cancellationToken)
    {
        var hospitalization = await context.ClientHospitalizations.SingleOrDefaultAsync(
            item => item.Id == request.Id && item.ClientId == request.ClientId,
            cancellationToken);
        if (hospitalization is null)
        {
            return new(HospitalizationMutationStatus.NotFound);
        }

        if (!string.Equals(
                ToEditorVersion(hospitalization.RowVersion),
                request.EditorVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            return new(HospitalizationMutationStatus.Conflict);
        }

        hospitalization.Update(
            request.HospitalName,
            request.AdmittedAtUtc,
            request.DischargedAtUtc,
            request.Reason,
            request.Diagnosis,
            request.DischargeInstructions,
            request.NowUtc);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(HospitalizationMutationStatus.Conflict);
        }

        return new(HospitalizationMutationStatus.Succeeded, Map(hospitalization));
    }

    private async Task<bool> CanAccessClientAsync(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        return tenant is not null
            && await context.Clients.AsNoTracking().AnyAsync(
                client => client.Id == clientId
                    && (tenant.IsPlatformAdministrator
                        || (client.CompanyId == tenant.CompanyId
                            && context.UserLocations.Any(access =>
                                access.UserId == tenant.UserId
                                && access.LocationId == client.LocationId))),
                cancellationToken);
    }

    private static HospitalizationItem Map(ClientHospitalization hospitalization) => new(
        hospitalization.Id,
        hospitalization.HospitalName,
        hospitalization.AdmittedAtUtc,
        hospitalization.DischargedAtUtc,
        hospitalization.Reason,
        hospitalization.Diagnosis,
        hospitalization.DischargeInstructions,
        ToEditorVersion(hospitalization.RowVersion));

    private static string ToEditorVersion(byte[] rowVersion) =>
        Convert.ToHexString(SHA256.HashData(rowVersion));
}
