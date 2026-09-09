using CareComply.Application.Abstractions.Clients;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CareComply.Application.Abstractions.Tenancy;

namespace CareComply.Infrastructure.Clients;

internal sealed class EfClientDirectory(
    CareComplyDbContext context,
    ITenantScopeAccessor tenantScopeAccessor) : IClientDirectory
{
    public async Task<ClientDirectoryPage> ListAsync(
        ClientDirectoryRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null) return new([], request.PageNumber, request.PageSize, 0);

        var clients = context.Clients.AsNoTracking().AsQueryable();
        if (!tenant.IsPlatformAdministrator)
        {
            clients = clients.Where(client => client.CompanyId == tenant.CompanyId
                && context.UserLocations.Any(access => access.UserId == tenant.UserId && access.LocationId == client.LocationId));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            clients = clients.Where(client =>
                client.FirstName.Contains(search)
                || client.MiddleName.Contains(search)
                || client.LastName.Contains(search)
                || client.PreferredName.Contains(search)
                || client.PmiNumber.Contains(search)
                || client.Email.Contains(search)
                || client.PrimaryPhone.Contains(search));
        }

        if (request.Status.HasValue)
        {
            clients = clients.Where(client => client.Status == request.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            var location = request.Location.Trim();
            clients = clients.Where(client => client.Location == location);
        }

        if (request.ProgramType.HasValue)
        {
            clients = clients.Where(client => context.ClientAdditionalProfiles.AsNoTracking().Any(profile =>
                profile.ClientId == client.Id
                && profile.ProgramType == request.ProgramType.Value));
        }

        if (request.IsOnboarding.HasValue)
        {
            clients = clients.Where(client => client.IsOnboarding == request.IsOnboarding.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.ServiceCode))
        {
            var serviceCode = request.ServiceCode.Trim();
            clients = clients.Where(client => context.ClientServices.AsNoTracking().Any(service =>
                service.ClientId == client.Id
                && service.IsActive
                && service.Code == serviceCode));
        }

        var totalCount = await clients.CountAsync(cancellationToken);
        clients = ApplySort(clients, request.Sort, request.SortDescending);

        var query = clients.Select(client => new ClientListProjection(
            client.Id,
            client.FirstName,
            client.MiddleName,
            client.LastName,
            client.PreferredName,
            client.PmiNumber,
            client.DateOfBirth,
            client.Gender,
            client.Status,
            client.Location,
            client.SocDate,
            client.IsOnboarding,
            context.ClientAdditionalProfiles.AsNoTracking()
                .Where(profile => profile.ClientId == client.Id)
                .Select(profile => profile.ProgramType)
                .FirstOrDefault(),
            client.UpdatedAtUtc,
            client.Email,
            client.PrimaryPhone));

        var rows = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToArrayAsync(cancellationToken);

        var clientIds = rows.Select(row => row.Id).ToArray();
        var services = clientIds.Length == 0
            ? []
            : await context.ClientServices.AsNoTracking()
                .Where(service => clientIds.Contains(service.ClientId) && service.IsActive)
                .OrderBy(service => service.Name)
                .Select(service => new ServiceProjection(
                    service.ClientId,
                    service.Id,
                    service.Code,
                    service.Name,
                    service.StartDate,
                    service.EndDate,
                    service.IsActive))
                .ToArrayAsync(cancellationToken);

        var servicesByClient = services
            .GroupBy(service => service.ClientId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ClientDirectoryService>)group
                    .Select(MapService)
                    .ToArray());

        var items = rows.Select(row => new ClientDirectoryItem(
            row.Id,
            row.FirstName,
            row.MiddleName,
            row.LastName,
            row.PreferredName,
            row.PmiNumber,
            row.DateOfBirth,
            row.Gender,
            row.Status,
            row.Location,
            row.SocDate,
            row.IsOnboarding,
            row.ProgramType,
            servicesByClient.GetValueOrDefault(row.Id, []),
            row.UpdatedAtUtc)).ToArray();

        return new(items, request.PageNumber, request.PageSize, totalCount);
    }

    public async Task<ClientDirectoryDetails?> GetByIdAsync(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null) return null;

        var client = await context.Clients.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == clientId
                && (tenant.IsPlatformAdministrator || (item.CompanyId == tenant.CompanyId
                    && context.UserLocations.Any(access => access.UserId == tenant.UserId && access.LocationId == item.LocationId))), cancellationToken);
        if (client is null)
        {
            return null;
        }

        var profile = await context.ClientAdditionalProfiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ClientId == clientId, cancellationToken);
        var services = await context.ClientServices.AsNoTracking()
            .Where(item => item.ClientId == clientId)
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.Name)
            .ThenByDescending(item => item.StartDate)
            .Select(item => new ClientDirectoryService(
                item.Id,
                item.Code,
                item.Name,
                item.StartDate,
                item.EndDate,
                item.IsActive))
            .ToArrayAsync(cancellationToken);
        var languages = await context.ClientLanguagePreferences.AsNoTracking()
            .Where(item => item.ClientId == clientId)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.LanguageName)
            .Select(item => new ClientDirectoryLanguage(
                item.LanguageCode,
                item.LanguageName,
                item.IsPrimary))
            .ToArrayAsync(cancellationToken);

        return new(
            client.Id,
            client.FirstName,
            client.MiddleName,
            client.LastName,
            client.PreferredName,
            client.DateOfBirth,
            client.Gender,
            client.PmiNumber,
            !string.IsNullOrWhiteSpace(client.ProtectedSsn),
            client.InsuranceProvider,
            client.Email,
            client.PrimaryPhone,
            client.SecondaryPhone,
            client.IntakeDate,
            client.SocDate,
            client.ReferralSource,
            client.Status,
            client.Location,
            client.IsOnboarding,
            client.DischargeDate,
            client.DischargeReason,
            client.AddressLine1,
            client.AddressLine2,
            client.City,
            client.State,
            client.PostalCode,
            client.County,
            client.Country,
            client.EvvAuthorizedHoursPerWeek,
            client.SafetyMeasures,
            client.SpecialEquipment,
            client.Allergies,
            client.OtherServices,
            MapProfile(profile),
            services,
            languages,
            client.CreatedAtUtc,
            client.UpdatedAtUtc,
            ClientEditorVersion.Create(client.RowVersion),
            client.LocationId);
    }

    private static IQueryable<Client> ApplySort(
        IQueryable<Client> query,
        ClientDirectorySort sort,
        bool descending) => (sort, descending) switch
    {
        (ClientDirectorySort.SocDate, true) => query.OrderByDescending(item => item.SocDate).ThenBy(item => item.LastName).ThenBy(item => item.FirstName),
        (ClientDirectorySort.SocDate, false) => query.OrderBy(item => item.SocDate).ThenBy(item => item.LastName).ThenBy(item => item.FirstName),
        (ClientDirectorySort.Status, true) => query.OrderByDescending(item => item.Status).ThenBy(item => item.LastName).ThenBy(item => item.FirstName),
        (ClientDirectorySort.Status, false) => query.OrderBy(item => item.Status).ThenBy(item => item.LastName).ThenBy(item => item.FirstName),
        (ClientDirectorySort.Location, true) => query.OrderByDescending(item => item.Location).ThenBy(item => item.LastName).ThenBy(item => item.FirstName),
        (ClientDirectorySort.Location, false) => query.OrderBy(item => item.Location).ThenBy(item => item.LastName).ThenBy(item => item.FirstName),
        (ClientDirectorySort.Updated, true) => query.OrderByDescending(item => item.UpdatedAtUtc).ThenBy(item => item.LastName).ThenBy(item => item.FirstName),
        (ClientDirectorySort.Updated, false) => query.OrderBy(item => item.UpdatedAtUtc).ThenBy(item => item.LastName).ThenBy(item => item.FirstName),
        (ClientDirectorySort.Name, true) => query.OrderByDescending(item => item.LastName).ThenByDescending(item => item.FirstName),
        _ => query.OrderBy(item => item.LastName).ThenBy(item => item.FirstName),
    };

    private static ClientDirectoryAdditionalProfile MapProfile(ClientAdditionalProfile? profile) =>
        profile is null
            ? new(
                ClientProgramType.None,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                null,
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty)
            : new(
                profile.ProgramType,
                profile.ReferralDetails,
                profile.HomeCareRating,
                profile.MedicalEquipment,
                profile.PcaComplexity,
                profile.Demographics,
                profile.Smokes,
                profile.IsVeteran,
                profile.HasPets,
                profile.LivingArrangement,
                profile.PreferredClinic,
                profile.PreferredHospital,
                profile.PreferredAmbulance,
                profile.CaregiverPreference,
                profile.NutritionalRequirements,
                profile.Diet,
                profile.MealSchedule,
                profile.Notes,
                profile.AdditionalInformation);

    private static ClientDirectoryService MapService(ServiceProjection service) => new(
        service.Id,
        service.Code,
        service.Name,
        service.StartDate,
        service.EndDate,
        service.IsActive);

    private sealed record ClientListProjection(
        Guid Id,
        string FirstName,
        string MiddleName,
        string LastName,
        string PreferredName,
        string PmiNumber,
        DateOnly DateOfBirth,
        ClientGender Gender,
        ClientStatus Status,
        string Location,
        DateOnly SocDate,
        bool IsOnboarding,
        ClientProgramType ProgramType,
        DateTimeOffset UpdatedAtUtc,
        string Email,
        string PrimaryPhone);

    private sealed record ServiceProjection(
        Guid ClientId,
        Guid Id,
        string Code,
        string Name,
        DateOnly StartDate,
        DateOnly? EndDate,
        bool IsActive);
}
