using CareComply.Application.Abstractions.Clients;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CareComply.Application.Abstractions.Tenancy;

namespace CareComply.Infrastructure.Clients;

internal sealed class EfClientAdministration(
    CareComplyDbContext context,
    IClientDirectory directory,
    IClientSensitiveData sensitiveData,
    ITenantScopeAccessor tenantScopeAccessor,
    TimeProvider timeProvider) : IClientAdministration
{
    public async Task<ClientAdministrationResult> CreateAsync(
        ClientCreateRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null) return Failed(ClientAdministrationStatus.NotFound);

        var pmiNumber = NormalizeRequired(request.Identity.PmiNumber);
        if (await context.Clients.AsNoTracking().AnyAsync(
                client => client.CompanyId == tenant.CompanyId && client.PmiNumber == pmiNumber,
                cancellationToken))
        {
            return Failed(ClientAdministrationStatus.DuplicatePmi);
        }

        var now = timeProvider.GetUtcNow();
        var clientId = Guid.CreateVersion7();

        try
        {
            var client = Client.Create(
                clientId,
                tenant.CompanyId,
                request.Identity.FirstName,
                request.Identity.MiddleName,
                request.Identity.LastName,
                request.Identity.PreferredName,
                request.Identity.DateOfBirth,
                request.Identity.Gender,
                pmiNumber,
                request.CareSetup.SocDate,
                request.CareSetup.IntakeDate,
                request.InitialStatus,
                now);

            if (!string.IsNullOrWhiteSpace(request.Identity.Ssn))
            {
                client.ChangeProtectedSsn(sensitiveData.Protect(request.Identity.Ssn), now);
            }

            client.ChangeContact(
                request.Contact.Email,
                request.Contact.PrimaryPhone,
                request.Contact.SecondaryPhone,
                request.Contact.AddressLine1,
                request.Contact.AddressLine2,
                request.Contact.City,
                request.Contact.State,
                request.Contact.PostalCode,
                request.Contact.County,
                request.Contact.Country,
                now);
            client.ChangeCareSetup(
                request.CareSetup.IntakeDate,
                request.CareSetup.SocDate,
                request.InitialStatus,
                request.CareSetup.ReferralSource,
                request.CareSetup.Location,
                request.Contact.InsuranceProvider,
                request.CareSetup.IsOnboarding,
                request.CareSetup.EvvAuthorizedHoursPerWeek,
                request.InitialStatus == ClientStatus.Discharged ? request.DischargeDate : null,
                request.InitialStatus == ClientStatus.Discharged ? request.DischargeReason : null,
                now);
            client.ChangeSafety(
                request.Safety.SafetyMeasures,
                request.Safety.SpecialEquipment,
                request.Safety.Allergies,
                request.Safety.OtherServices,
                now);

            var profile = new ClientAdditionalProfile(clientId);
            ApplyProfile(profile, request.AdditionalProfile);

            var services = request.Services
                .Select(service => new ClientService(
                    Guid.CreateVersion7(),
                    clientId,
                    service.Code,
                    service.Name,
                    request.CareSetup.SocDate))
                .ToArray();
            var languages = request.Languages
                .Select(language => new ClientLanguagePreference(
                    clientId,
                    language.LanguageCode,
                    language.LanguageName,
                    language.IsPrimary))
                .ToArray();

            context.Clients.Add(client);
            context.ClientAdditionalProfiles.Add(profile);
            context.ClientServices.AddRange(services);
            context.ClientLanguagePreferences.AddRange(languages);
            await context.SaveChangesAsync(cancellationToken);

            return new(
                ClientAdministrationStatus.Succeeded,
                await GetRequiredDetailsAsync(clientId, cancellationToken),
                ["Core", "AdditionalProfile", "Services", "Languages"]);
        }
        catch (DbUpdateException exception) when (IsConstraint(exception, "UX_Clients_PmiNumber"))
        {
            return Failed(ClientAdministrationStatus.DuplicatePmi);
        }
        catch (DbUpdateException exception) when (IsServiceConstraint(exception))
        {
            return Failed(ClientAdministrationStatus.ServiceHistoryConflict);
        }
        catch (ArgumentException)
        {
            return Failed(ClientAdministrationStatus.InvalidState);
        }
    }

    public async Task<ClientAdministrationResult> UpdateAsync(
        ClientUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null) return Failed(ClientAdministrationStatus.NotFound);

        var client = await context.Clients.SingleOrDefaultAsync(
            item => item.Id == request.ClientId
                && (tenant.IsPlatformAdministrator || item.CompanyId == tenant.CompanyId),
            cancellationToken);
        if (client is null)
        {
            return Failed(ClientAdministrationStatus.NotFound);
        }

        if (!ClientEditorVersion.Matches(client.RowVersion, request.EditorVersion))
        {
            return Failed(ClientAdministrationStatus.Conflict);
        }

        var pmiNumber = NormalizeRequired(request.Identity.PmiNumber);
        if (!string.Equals(client.PmiNumber, pmiNumber, StringComparison.OrdinalIgnoreCase)
            && await context.Clients.AsNoTracking().AnyAsync(
                item => item.Id != request.ClientId
                    && item.CompanyId == client.CompanyId
                    && item.PmiNumber == pmiNumber,
                cancellationToken))
        {
            return Failed(ClientAdministrationStatus.DuplicatePmi);
        }

        var profile = await context.ClientAdditionalProfiles.SingleOrDefaultAsync(
            item => item.ClientId == request.ClientId,
            cancellationToken);
        var activeServices = await context.ClientServices
            .Where(item => item.ClientId == request.ClientId && item.IsActive)
            .ToListAsync(cancellationToken);
        var languages = await context.ClientLanguagePreferences
            .Where(item => item.ClientId == request.ClientId)
            .ToListAsync(cancellationToken);

        var changedSections = new List<string>(6);
        var identityChanged = IdentityChanged(client, request.Identity);
        var ssnChanged = !SsnMatches(client.ProtectedSsn, request.Identity.Ssn, sensitiveData);
        var contactChanged = ContactChanged(client, request.Contact);
        var careChanged = CareChanged(client, request.CareSetup, request.Contact.InsuranceProvider);
        var safetyChanged = SafetyChanged(client, request.Safety);
        var profileChanged = ProfileChanged(profile, request.AdditionalProfile);
        var serviceChange = AnalyzeServiceChanges(activeServices, request.Services);
        var languagesChanged = LanguagesChanged(languages, request.Languages);

        if (serviceChange.ActiveSetChanged && !request.ServiceChangeEffectiveDate.HasValue)
        {
            return Failed(ClientAdministrationStatus.InvalidServiceChange);
        }

        if (request.ServiceChangeEffectiveDate.HasValue
            && serviceChange.ServicesToEnd.Any(service =>
                request.ServiceChangeEffectiveDate.Value < service.StartDate))
        {
            return Failed(ClientAdministrationStatus.InvalidServiceChange);
        }

        if (!identityChanged
            && !ssnChanged
            && !contactChanged
            && !careChanged
            && !safetyChanged
            && !profileChanged
            && !serviceChange.HasChanges
            && !languagesChanged)
        {
            return new(
                ClientAdministrationStatus.NoChanges,
                await GetRequiredDetailsAsync(request.ClientId, cancellationToken),
                []);
        }

        var now = timeProvider.GetUtcNow();

        try
        {
            if (identityChanged)
            {
                client.ChangeIdentity(
                    request.Identity.FirstName,
                    request.Identity.MiddleName,
                    request.Identity.LastName,
                    request.Identity.PreferredName,
                    request.Identity.DateOfBirth,
                    request.Identity.Gender,
                    pmiNumber,
                    now);
                changedSections.Add("Identity");
            }

            if (ssnChanged)
            {
                client.ChangeProtectedSsn(
                    string.IsNullOrWhiteSpace(request.Identity.Ssn) ? null : sensitiveData.Protect(request.Identity.Ssn),
                    now);
                if (!changedSections.Contains("Identity", StringComparer.Ordinal)) changedSections.Add("Identity");
            }

            if (contactChanged)
            {
                client.ChangeContact(
                    request.Contact.Email,
                    request.Contact.PrimaryPhone,
                    request.Contact.SecondaryPhone,
                    request.Contact.AddressLine1,
                    request.Contact.AddressLine2,
                    request.Contact.City,
                request.Contact.State,
                request.Contact.PostalCode,
                request.Contact.County,
                request.Contact.Country,
                now);
                changedSections.Add("Contact");
            }

            if (careChanged)
            {
                client.ChangeCareSetup(
                    request.CareSetup.IntakeDate,
                    request.CareSetup.SocDate,
                    client.Status,
                    request.CareSetup.ReferralSource,
                    request.CareSetup.Location,
                    request.Contact.InsuranceProvider,
                    request.CareSetup.IsOnboarding,
                    request.CareSetup.EvvAuthorizedHoursPerWeek,
                    client.DischargeDate,
                    client.DischargeReason,
                    now);
                changedSections.Add("CareSetup");
            }

            if (safetyChanged)
            {
                client.ChangeSafety(
                    request.Safety.SafetyMeasures,
                    request.Safety.SpecialEquipment,
                    request.Safety.Allergies,
                    request.Safety.OtherServices,
                    now);
                changedSections.Add("Safety");
            }

            if (profileChanged)
            {
                profile ??= new ClientAdditionalProfile(request.ClientId);
                if (context.Entry(profile).State == EntityState.Detached)
                {
                    context.ClientAdditionalProfiles.Add(profile);
                }

                ApplyProfile(profile, request.AdditionalProfile);
                client.MarkUpdated(now);
                changedSections.Add("AdditionalProfile");
            }

            if (serviceChange.HasChanges)
            {
                ApplyServiceChanges(
                    request.ClientId,
                    activeServices,
                    request.Services,
                    serviceChange,
                    request.ServiceChangeEffectiveDate,
                    now,
                    client);
                changedSections.Add("Services");
            }

            if (languagesChanged)
            {
                // Remove the old primary preference in a separate database statement so the
                // filtered unique index can never observe two primary rows during replacement.
                if (languages.Count > 0)
                {
                    context.ClientLanguagePreferences.RemoveRange(languages);
                    client.MarkUpdated(now);
                    await context.SaveChangesAsync(cancellationToken);
                }

                context.ClientLanguagePreferences.AddRange(request.Languages.Select(language =>
                    new ClientLanguagePreference(
                        request.ClientId,
                        language.LanguageCode,
                        language.LanguageName,
                        language.IsPrimary)));
                client.MarkUpdated(now);
                changedSections.Add("Languages");
            }

            await context.SaveChangesAsync(cancellationToken);
            return new(
                ClientAdministrationStatus.Succeeded,
                await GetRequiredDetailsAsync(request.ClientId, cancellationToken),
                changedSections);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failed(ClientAdministrationStatus.Conflict);
        }
        catch (DbUpdateException exception) when (IsConstraint(exception, "UX_Clients_PmiNumber"))
        {
            return Failed(ClientAdministrationStatus.DuplicatePmi);
        }
        catch (DbUpdateException exception) when (IsServiceConstraint(exception))
        {
            return Failed(ClientAdministrationStatus.ServiceHistoryConflict);
        }
        catch (ArgumentException)
        {
            return Failed(ClientAdministrationStatus.InvalidState);
        }
    }

    public async Task<ClientAdministrationResult> ChangeStatusAsync(
        ClientStatusChangeRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null) return Failed(ClientAdministrationStatus.NotFound);

        var client = await context.Clients.SingleOrDefaultAsync(
            item => item.Id == request.ClientId
                && (tenant.IsPlatformAdministrator || item.CompanyId == tenant.CompanyId),
            cancellationToken);
        if (client is null)
        {
            return Failed(ClientAdministrationStatus.NotFound);
        }

        if (!ClientEditorVersion.Matches(client.RowVersion, request.EditorVersion))
        {
            return Failed(ClientAdministrationStatus.Conflict);
        }

        var previousStatus = client.Status;
        var requestedDischargeDate = request.Status == ClientStatus.Discharged
            ? request.DischargeDate
            : null;
        var requestedDischargeReason = request.Status == ClientStatus.Discharged
            ? Normalize(request.DischargeReason)
            : string.Empty;

        if (client.Status == request.Status
            && client.DischargeDate == requestedDischargeDate
            && string.Equals(
                client.DischargeReason,
                requestedDischargeReason,
                StringComparison.Ordinal))
        {
            return new(
                ClientAdministrationStatus.NoChanges,
                await GetRequiredDetailsAsync(request.ClientId, cancellationToken),
                [],
                previousStatus);
        }

        try
        {
            client.ChangeStatus(
                request.Status,
                requestedDischargeDate,
                requestedDischargeReason,
                timeProvider.GetUtcNow());
            await context.SaveChangesAsync(cancellationToken);

            return new(
                ClientAdministrationStatus.Succeeded,
                await GetRequiredDetailsAsync(request.ClientId, cancellationToken),
                ["Status"],
                previousStatus);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(ClientAdministrationStatus.Conflict, null, [], previousStatus);
        }
        catch (ArgumentException)
        {
            return new(ClientAdministrationStatus.InvalidState, null, [], previousStatus);
        }
    }

    private async Task<ClientDirectoryDetails> GetRequiredDetailsAsync(
        Guid clientId,
        CancellationToken cancellationToken) =>
        await directory.GetByIdAsync(clientId, cancellationToken)
        ?? throw new InvalidOperationException("The client could not be reloaded after a successful mutation.");

    private static void ApplyProfile(
        ClientAdditionalProfile profile,
        ClientAdditionalProfileInput input) =>
        profile.Change(
            input.ProgramType,
            input.ReferralDetails,
            input.HomeCareRating,
            input.MedicalEquipment,
            input.PcaComplexity,
            input.Demographics,
            input.Smokes,
            input.IsVeteran,
            input.HasPets,
            input.LivingArrangement,
            input.PreferredClinic,
            input.PreferredHospital,
            input.PreferredAmbulance,
            input.CaregiverPreference,
            input.NutritionalRequirements,
            input.Diet,
            input.MealSchedule,
            input.Notes,
            input.AdditionalInformation);

    private static bool IdentityChanged(Client client, ClientIdentityInput input) =>
        !Equal(client.FirstName, input.FirstName)
        || !Equal(client.MiddleName, input.MiddleName)
        || !Equal(client.LastName, input.LastName)
        || !Equal(client.PreferredName, input.PreferredName)
        || client.DateOfBirth != input.DateOfBirth
        || client.Gender != input.Gender
        || !Equal(client.PmiNumber, input.PmiNumber, StringComparison.OrdinalIgnoreCase);

    private static bool ContactChanged(Client client, ClientContactInput input) =>
        !Equal(client.Email, input.Email, StringComparison.OrdinalIgnoreCase)
        || !Equal(client.PrimaryPhone, input.PrimaryPhone)
        || !Equal(client.SecondaryPhone, input.SecondaryPhone)
        || !Equal(client.AddressLine1, input.AddressLine1)
        || !Equal(client.AddressLine2, input.AddressLine2)
        || !Equal(client.City, input.City)
        || !Equal(client.State, input.State)
        || !Equal(client.PostalCode, input.PostalCode)
        || !Equal(client.County, input.County)
        || !Equal(client.Country, input.Country);

    private static bool SsnMatches(
        string protectedSsn,
        string? requestedSsn,
        IClientSensitiveData sensitiveData)
    {
        // Omitted SSN means "leave unchanged" so ordinary client edits cannot clear protected data.
        if (requestedSsn is null) return true;
        if (string.IsNullOrWhiteSpace(protectedSsn)) return string.IsNullOrWhiteSpace(requestedSsn);
        if (string.IsNullOrWhiteSpace(requestedSsn)) return false;
        try
        {
            return string.Equals(sensitiveData.Unprotect(protectedSsn), requestedSsn, StringComparison.Ordinal);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }

    private static bool CareChanged(
        Client client,
        ClientCareSetupInput input,
        string? insuranceProvider) =>
        client.IntakeDate != input.IntakeDate
        || client.SocDate != input.SocDate
        || !Equal(client.ReferralSource, input.ReferralSource)
        || !Equal(client.Location, input.Location)
        || !Equal(client.InsuranceProvider, insuranceProvider)
        || client.IsOnboarding != input.IsOnboarding
        || client.EvvAuthorizedHoursPerWeek != input.EvvAuthorizedHoursPerWeek;

    private static bool SafetyChanged(Client client, ClientSafetyInput input) =>
        !Equal(client.SafetyMeasures, input.SafetyMeasures)
        || !Equal(client.SpecialEquipment, input.SpecialEquipment)
        || !Equal(client.Allergies, input.Allergies)
        || !Equal(client.OtherServices, input.OtherServices);

    private static bool ProfileChanged(
        ClientAdditionalProfile? profile,
        ClientAdditionalProfileInput input)
    {
        if (profile is null)
        {
            return true;
        }

        return profile.ProgramType != input.ProgramType
            || !Equal(profile.ReferralDetails, input.ReferralDetails)
            || !Equal(profile.HomeCareRating, input.HomeCareRating)
            || !Equal(profile.MedicalEquipment, input.MedicalEquipment)
            || !Equal(profile.PcaComplexity, input.PcaComplexity)
            || !Equal(profile.Demographics, input.Demographics)
            || profile.Smokes != input.Smokes
            || profile.IsVeteran != input.IsVeteran
            || profile.HasPets != input.HasPets
            || !Equal(profile.LivingArrangement, input.LivingArrangement)
            || !Equal(profile.PreferredClinic, input.PreferredClinic)
            || !Equal(profile.PreferredHospital, input.PreferredHospital)
            || !Equal(profile.PreferredAmbulance, input.PreferredAmbulance)
            || !Equal(profile.CaregiverPreference, input.CaregiverPreference)
            || !Equal(profile.NutritionalRequirements, input.NutritionalRequirements)
            || !Equal(profile.Diet, input.Diet)
            || !Equal(profile.MealSchedule, input.MealSchedule)
            || !Equal(profile.Notes, input.Notes)
            || !Equal(profile.AdditionalInformation, input.AdditionalInformation);
    }

    private static ServiceChangeAnalysis AnalyzeServiceChanges(
        IReadOnlyList<ClientService> activeServices,
        IReadOnlyList<ClientServiceInput> requestedServices)
    {
        var currentByCode = activeServices.ToDictionary(
            service => service.Code,
            StringComparer.OrdinalIgnoreCase);
        var requestedByCode = requestedServices.ToDictionary(
            service => service.Code.Trim(),
            StringComparer.OrdinalIgnoreCase);

        var toEnd = activeServices
            .Where(service => !requestedByCode.ContainsKey(service.Code))
            .ToArray();
        var toAdd = requestedServices
            .Where(service => !currentByCode.ContainsKey(service.Code.Trim()))
            .ToArray();
        var toRename = activeServices
            .Where(service => requestedByCode.TryGetValue(service.Code, out var requested)
                && !Equal(service.Name, requested.Name))
            .ToArray();

        return new(
            toEnd,
            toAdd,
            toRename,
            toEnd.Length > 0 || toAdd.Length > 0,
            toEnd.Length > 0 || toAdd.Length > 0 || toRename.Length > 0);
    }

    private void ApplyServiceChanges(
        Guid clientId,
        IReadOnlyList<ClientService> activeServices,
        IReadOnlyList<ClientServiceInput> requestedServices,
        ServiceChangeAnalysis analysis,
        DateOnly? effectiveDate,
        DateTimeOffset now,
        Client client)
    {
        var requestedByCode = requestedServices.ToDictionary(
            service => service.Code.Trim(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var service in analysis.ServicesToRename)
        {
            service.Rename(requestedByCode[service.Code].Name);
        }

        if (analysis.ActiveSetChanged)
        {
            var changeDate = effectiveDate
                ?? throw new InvalidOperationException("A service change date is required for an active service set change.");

            foreach (var service in analysis.ServicesToEnd)
            {
                service.End(changeDate);
            }

            foreach (var service in analysis.ServicesToAdd)
            {
                context.ClientServices.Add(new ClientService(
                    Guid.CreateVersion7(),
                    clientId,
                    service.Code,
                    service.Name,
                    changeDate));
            }
        }

        client.MarkUpdated(now);
    }

    private static bool LanguagesChanged(
        List<ClientLanguagePreference> current,
        IReadOnlyList<ClientLanguageInput> requested)
    {
        if (current.Count != requested.Count)
        {
            return true;
        }

        var requestedByCode = requested.ToDictionary(
            language => language.LanguageCode.Trim(),
            StringComparer.OrdinalIgnoreCase);
        foreach (var language in current)
        {
            if (!requestedByCode.TryGetValue(language.LanguageCode, out var desired)
                || !Equal(language.LanguageName, desired.LanguageName)
                || language.IsPrimary != desired.IsPrimary)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Equal(
        string? current,
        string? requested,
        StringComparison comparison = StringComparison.Ordinal) =>
        string.Equals(
            Normalize(current),
            Normalize(requested),
            comparison);

    private static string Normalize(string? value) => (value ?? string.Empty).Trim();

    private static string NormalizeRequired(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }

    private static bool IsServiceConstraint(DbUpdateException exception) =>
        IsConstraint(exception, "UX_ClientServices_Client_Code_StartDate")
        || IsConstraint(exception, "UX_ClientServices_Client_Code_Active");

    private static bool IsConstraint(DbUpdateException exception, string constraintName) =>
        exception.GetBaseException().Message.Contains(
            constraintName,
            StringComparison.OrdinalIgnoreCase);

    private static ClientAdministrationResult Failed(ClientAdministrationStatus status) =>
        new(status, null, []);

    private sealed record ServiceChangeAnalysis(
        IReadOnlyList<ClientService> ServicesToEnd,
        IReadOnlyList<ClientServiceInput> ServicesToAdd,
        IReadOnlyList<ClientService> ServicesToRename,
        bool ActiveSetChanged,
        bool HasChanges);
}
