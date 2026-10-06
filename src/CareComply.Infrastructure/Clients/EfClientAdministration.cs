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
    ClientAssignmentResolver assignmentResolver,
    ClientBasicDetailsPersistence basicDetailsPersistence,
    ClientChangeDetector changeDetector,
    ClientServiceSynchronizer serviceSynchronizer,
    ClientLanguageSynchronizer languageSynchronizer,
    ClientContactInformationPersistence contactInformationPersistence,
    TimeProvider timeProvider) : IClientAdministration
{
    public async Task<ClientAdministrationResult> CreateBasicDetailsAsync(
        ClientBasicDetailsCreateRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant?.CompanyId is null) return Failed(ClientAdministrationStatus.NotFound);

        try
        {
            var preparation = await basicDetailsPersistence.PrepareAsync(
                tenant.CompanyId.Value,
                request,
                cancellationToken);
            if (preparation.Client is null)
            {
                return Failed(preparation.Status);
            }

            await context.SaveChangesAsync(cancellationToken);

            return new(
                ClientAdministrationStatus.Succeeded,
                await GetRequiredDetailsAsync(preparation.Client.Id, cancellationToken),
                ["Core", "Services"]);
        }
        catch (DbUpdateException exception) when (ClientPersistenceErrorClassifier.IsDuplicatePmi(exception))
        {
            return Failed(ClientAdministrationStatus.DuplicatePmi);
        }
        catch (DbUpdateException exception) when (ClientPersistenceErrorClassifier.IsServiceHistoryConflict(exception))
        {
            return Failed(ClientAdministrationStatus.ServiceHistoryConflict);
        }
        catch (ArgumentException)
        {
            return Failed(ClientAdministrationStatus.InvalidState);
        }
    }

    public async Task<ClientAdministrationResult> CreateAsync(
        ClientCreateRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant?.CompanyId is null) return Failed(ClientAdministrationStatus.NotFound);

        var location = await assignmentResolver.ResolveLocationAsync(
            tenant.CompanyId.Value,
            request.CareSetup.LocationId,
            request.CareSetup.Location,
            cancellationToken);
        if (location is null) return Failed(ClientAdministrationStatus.InvalidState);

        if (!await assignmentResolver.IsValidSupervisorAsync(
                request.CareSetup.SupervisorUserId,
                tenant.CompanyId.Value,
                location.Id,
                cancellationToken))
            return Failed(ClientAdministrationStatus.InvalidState);

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
                tenant.CompanyId.Value,
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
                location.Name,
                request.Contact.InsuranceProvider,
                request.CareSetup.IsOnboarding,
                request.CareSetup.EvvAuthorizedHoursPerWeek,
                request.InitialStatus == ClientStatus.Discharged ? request.DischargeDate : null,
                request.InitialStatus == ClientStatus.Discharged ? request.DischargeReason : null,
                now);
            client.AssignLocation(location.Id, location.Name, now);
            client.AssignSupervisor(request.CareSetup.SupervisorUserId, now);
            client.ChangeSafety(
                request.Safety.SafetyMeasures,
                request.Safety.SpecialEquipment,
                request.Safety.Allergies,
                request.Safety.OtherServices,
                now);

            var profile = new ClientAdditionalProfile(clientId);
            ClientProfilePersistence.Apply(profile, request.AdditionalProfile);

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
        catch (DbUpdateException exception) when (ClientPersistenceErrorClassifier.IsDuplicatePmi(exception))
        {
            return Failed(ClientAdministrationStatus.DuplicatePmi);
        }
        catch (DbUpdateException exception) when (ClientPersistenceErrorClassifier.IsServiceHistoryConflict(exception))
        {
            return Failed(ClientAdministrationStatus.ServiceHistoryConflict);
        }
        catch (ArgumentException)
        {
            return Failed(ClientAdministrationStatus.InvalidState);
        }
    }

    public async Task<ClientAdministrationResult> UpdateBasicDetailsAsync(
        ClientBasicDetailsUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null) return Failed(ClientAdministrationStatus.NotFound);

        var client = await context.Clients.SingleOrDefaultAsync(
            item => item.Id == request.ClientId
                && (tenant.IsPlatformAdministrator || (item.CompanyId == tenant.CompanyId
                    && context.UserLocations.Any(access =>
                        access.UserId == tenant.UserId && access.LocationId == item.LocationId))),
            cancellationToken);
        if (client is null) return Failed(ClientAdministrationStatus.NotFound);
        if (client.Status != ClientStatus.Pending) return Failed(ClientAdministrationStatus.InvalidState);
        if (!ClientEditorVersion.Matches(client.RowVersion, request.EditorVersion))
            return Failed(ClientAdministrationStatus.Conflict);

        var location = await assignmentResolver.ResolveLocationAsync(
            client.CompanyId,
            request.CareSetup.LocationId,
            request.CareSetup.Location,
            cancellationToken);
        if (location is null) return Failed(ClientAdministrationStatus.InvalidState);

        if (!tenant.IsPlatformAdministrator && !await context.UserLocations.AsNoTracking().AnyAsync(
                access => access.UserId == tenant.UserId && access.LocationId == location.Id,
                cancellationToken))
            return Failed(ClientAdministrationStatus.NotFound);

        if (!await assignmentResolver.IsValidSupervisorAsync(
                request.CareSetup.SupervisorUserId,
                client.CompanyId,
                location.Id,
                cancellationToken))
            return Failed(ClientAdministrationStatus.InvalidState);

        var pmiNumber = NormalizeRequired(request.Identity.PmiNumber);
        if (!string.Equals(client.PmiNumber, pmiNumber, StringComparison.OrdinalIgnoreCase)
            && await context.Clients.AsNoTracking().AnyAsync(
                item => item.Id != request.ClientId
                    && item.CompanyId == client.CompanyId
                    && item.PmiNumber == pmiNumber,
                cancellationToken))
            return Failed(ClientAdministrationStatus.DuplicatePmi);

        var activeServices = await context.ClientServices
            .Where(item => item.ClientId == request.ClientId && item.IsActive)
            .ToListAsync(cancellationToken);
        var requestedSsn = string.IsNullOrWhiteSpace(request.Identity.Ssn) ? null : request.Identity.Ssn;
        var identityChanged = ClientChangeDetector.IdentityChanged(client, request.Identity);
        var ssnChanged = changeDetector.SsnChanged(client, requestedSsn);
        var careChanged = ClientChangeDetector.CareChanged(client, request.CareSetup, request.InsuranceProvider)
            || client.SupervisorUserId != request.CareSetup.SupervisorUserId
            || client.LocationId != location.Id;
        var serviceChange = ClientServiceSynchronizer.Analyze(activeServices, request.Services);

        if (serviceChange.ServicesToEnd.Any(service => request.CareSetup.SocDate < service.StartDate))
            return Failed(ClientAdministrationStatus.InvalidServiceChange);

        if (!identityChanged && !ssnChanged && !careChanged && !serviceChange.HasChanges)
            return new(
                ClientAdministrationStatus.NoChanges,
                await GetRequiredDetailsAsync(request.ClientId, cancellationToken),
                []);

        var now = timeProvider.GetUtcNow();
        var changedSections = new List<string>(2);

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
                changedSections.Add("BasicDetails");
            }

            if (ssnChanged)
            {
                client.ChangeProtectedSsn(sensitiveData.Protect(requestedSsn!), now);
                if (!changedSections.Contains("BasicDetails", StringComparer.Ordinal))
                    changedSections.Add("BasicDetails");
            }

            if (careChanged)
            {
                client.ChangeCareSetup(
                    request.CareSetup.IntakeDate,
                    request.CareSetup.SocDate,
                    ClientStatus.Pending,
                    request.CareSetup.ReferralSource,
                    location.Name,
                    request.InsuranceProvider,
                    request.CareSetup.IsOnboarding,
                    request.CareSetup.EvvAuthorizedHoursPerWeek,
                    null,
                    null,
                    now);
                client.AssignLocation(location.Id, location.Name, now);
                client.AssignSupervisor(request.CareSetup.SupervisorUserId, now);
                if (!changedSections.Contains("BasicDetails", StringComparer.Ordinal))
                    changedSections.Add("BasicDetails");
            }

            if (serviceChange.HasChanges)
            {
                serviceSynchronizer.Apply(
                    request.ClientId,
                    request.Services,
                    serviceChange,
                    request.CareSetup.SocDate,
                    now,
                    client);
                changedSections.Add("Services");
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
        catch (DbUpdateException exception) when (ClientPersistenceErrorClassifier.IsDuplicatePmi(exception))
        {
            return Failed(ClientAdministrationStatus.DuplicatePmi);
        }
        catch (DbUpdateException exception) when (ClientPersistenceErrorClassifier.IsServiceHistoryConflict(exception))
        {
            return Failed(ClientAdministrationStatus.ServiceHistoryConflict);
        }
        catch (ArgumentException)
        {
            return Failed(ClientAdministrationStatus.InvalidState);
        }
    }

    public async Task<ClientAdministrationResult> UpdateAdditionalInformationAsync(
        ClientAdditionalInformationUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null) return Failed(ClientAdministrationStatus.NotFound);

        var client = await context.Clients.SingleOrDefaultAsync(
            item => item.Id == request.ClientId
                && (tenant.IsPlatformAdministrator || (item.CompanyId == tenant.CompanyId
                    && context.UserLocations.Any(access =>
                        access.UserId == tenant.UserId && access.LocationId == item.LocationId))),
            cancellationToken);
        if (client is null) return Failed(ClientAdministrationStatus.NotFound);
        var status = await ClientAdditionalInformationPersistence.ApplyAsync(
            context,
            client,
            request.AdditionalProfile,
            request.EditorVersion,
            timeProvider.GetUtcNow(),
            cancellationToken);
        return status is ClientAdministrationStatus.Succeeded or ClientAdministrationStatus.NoChanges
            ? new(
                status,
                await GetRequiredDetailsAsync(request.ClientId, cancellationToken),
                status == ClientAdministrationStatus.Succeeded ? ["AdditionalProfile"] : [])
            : Failed(status);
    }

    public async Task<ClientAdministrationResult> UpdateContactInformationAsync(
        ClientContactInformationUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null) return Failed(ClientAdministrationStatus.NotFound);

        var client = await context.Clients.SingleOrDefaultAsync(
            item => item.Id == request.ClientId
                && (tenant.IsPlatformAdministrator || (item.CompanyId == tenant.CompanyId
                    && context.UserLocations.Any(access =>
                        access.UserId == tenant.UserId && access.LocationId == item.LocationId))),
            cancellationToken);
        if (client is null) return Failed(ClientAdministrationStatus.NotFound);
        var persistence = await contactInformationPersistence.ApplyAsync(
            client, request.Contact, request.Languages, request.EditorVersion,
            timeProvider.GetUtcNow(), cancellationToken);
        if (persistence.Status is not (ClientAdministrationStatus.Succeeded or ClientAdministrationStatus.NoChanges))
            return Failed(persistence.Status);

        var changedSections = new List<string>();
        if (persistence.ContactChanged) changedSections.Add("ContactInformation");
        if (persistence.LanguagesChanged) changedSections.Add("Languages");
        return new(
            persistence.Status,
            await GetRequiredDetailsAsync(request.ClientId, cancellationToken),
            changedSections);
    }

    public async Task<ClientAdministrationResult> UpdateAsync(
        ClientUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null) return Failed(ClientAdministrationStatus.NotFound);

        var client = await context.Clients.SingleOrDefaultAsync(
            item => item.Id == request.ClientId
                && (tenant.IsPlatformAdministrator || (item.CompanyId == tenant.CompanyId
                    && context.UserLocations.Any(access => access.UserId == tenant.UserId && access.LocationId == item.LocationId))),
            cancellationToken);
        if (client is null)
        {
            return Failed(ClientAdministrationStatus.NotFound);
        }

        var location = await assignmentResolver.ResolveLocationAsync(
            client.CompanyId,
            request.CareSetup.LocationId,
            request.CareSetup.Location,
            cancellationToken);
        if (location is null) return Failed(ClientAdministrationStatus.InvalidState);

        if (!tenant.IsPlatformAdministrator && !await context.UserLocations.AsNoTracking().AnyAsync(
                access => access.UserId == tenant.UserId && access.LocationId == location.Id,
                cancellationToken))
            return Failed(ClientAdministrationStatus.NotFound);

        if (!(request.CareSetup.SupervisorUserId == client.SupervisorUserId && location.Id == client.LocationId) &&
            !await assignmentResolver.IsValidSupervisorAsync(
                request.CareSetup.SupervisorUserId,
                client.CompanyId,
                location.Id,
                cancellationToken))
            return Failed(ClientAdministrationStatus.InvalidState);

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
        var identityChanged = ClientChangeDetector.IdentityChanged(client, request.Identity);
        var ssnChanged = changeDetector.SsnChanged(client, request.Identity.Ssn);
        var contactChanged = ClientChangeDetector.ContactChanged(client, request.Contact);
        var careChanged = ClientChangeDetector.CareChanged(client, request.CareSetup, request.Contact.InsuranceProvider)
            || client.SupervisorUserId != request.CareSetup.SupervisorUserId
            || client.LocationId != location.Id;
        var safetyChanged = ClientChangeDetector.SafetyChanged(client, request.Safety);
        var profileChanged = ClientProfilePersistence.HasChanges(profile, request.AdditionalProfile);
        var serviceChange = ClientServiceSynchronizer.Analyze(activeServices, request.Services);
        var languagesChanged = ClientLanguageSynchronizer.HasChanges(languages, request.Languages);

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
                    location.Name,
                    request.Contact.InsuranceProvider,
                    request.CareSetup.IsOnboarding,
                    request.CareSetup.EvvAuthorizedHoursPerWeek,
                    client.DischargeDate,
                    client.DischargeReason,
                    now);
                client.AssignLocation(location.Id, location.Name, now);
                client.AssignSupervisor(request.CareSetup.SupervisorUserId, now);
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

                ClientProfilePersistence.Apply(profile, request.AdditionalProfile);
                client.MarkUpdated(now);
                changedSections.Add("AdditionalProfile");
            }

            if (serviceChange.HasChanges)
            {
                serviceSynchronizer.Apply(
                    request.ClientId,
                    request.Services,
                    serviceChange,
                    request.ServiceChangeEffectiveDate,
                    now,
                    client);
                changedSections.Add("Services");
            }

            if (languagesChanged)
            {
                await languageSynchronizer.ReplaceAsync(
                    request.ClientId,
                    languages,
                    request.Languages,
                    client,
                    now,
                    cancellationToken);
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
        catch (DbUpdateException exception) when (ClientPersistenceErrorClassifier.IsDuplicatePmi(exception))
        {
            return Failed(ClientAdministrationStatus.DuplicatePmi);
        }
        catch (DbUpdateException exception) when (ClientPersistenceErrorClassifier.IsServiceHistoryConflict(exception))
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
                && (tenant.IsPlatformAdministrator || (item.CompanyId == tenant.CompanyId
                    && context.UserLocations.Any(access => access.UserId == tenant.UserId && access.LocationId == item.LocationId))),
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

    public async Task<ClientAdministrationResult> CompleteCreationAsync(
        ClientCreationCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null) return Failed(ClientAdministrationStatus.NotFound);

        var client = await context.Clients.SingleOrDefaultAsync(
            item => item.Id == request.ClientId
                && (tenant.IsPlatformAdministrator || (item.CompanyId == tenant.CompanyId
                    && context.UserLocations.Any(access => access.UserId == tenant.UserId && access.LocationId == item.LocationId))),
            cancellationToken);
        if (client is null) return Failed(ClientAdministrationStatus.NotFound);

        if (!ClientEditorVersion.Matches(client.RowVersion, request.EditorVersion))
        {
            return Failed(ClientAdministrationStatus.Conflict);
        }

        if (client.Status != ClientStatus.Pending)
        {
            return new(ClientAdministrationStatus.InvalidState, null, [], client.Status);
        }

        try
        {
            client.ChangeStatus(
                request.Status,
                request.Status == ClientStatus.Discharged ? request.DischargeDate : null,
                request.Status == ClientStatus.Discharged ? request.DischargeReason : null,
                timeProvider.GetUtcNow());
            await context.SaveChangesAsync(cancellationToken);

            return new(
                ClientAdministrationStatus.Succeeded,
                await GetRequiredDetailsAsync(request.ClientId, cancellationToken),
                ["Status"],
                ClientStatus.Pending);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(ClientAdministrationStatus.Conflict, null, [], ClientStatus.Pending);
        }
        catch (ArgumentException)
        {
            return new(ClientAdministrationStatus.InvalidState, null, [], ClientStatus.Pending);
        }
    }

    public async Task<ClientCreationStateResult> GetCreationStateAsync(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null) return new(ClientAdministrationStatus.NotFound);

        var client = await context.Clients.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == clientId
                && (tenant.IsPlatformAdministrator || (item.CompanyId == tenant.CompanyId
                    && context.UserLocations.Any(access =>
                        access.UserId == tenant.UserId && access.LocationId == item.LocationId))),
            cancellationToken);
        if (client is null) return new(ClientAdministrationStatus.NotFound);
        if (client.Status != ClientStatus.Pending)
            return new(ClientAdministrationStatus.InvalidState);

        var details = await GetRequiredDetailsAsync(clientId, cancellationToken);
        return new(
            ClientAdministrationStatus.Succeeded,
            new(details, Math.Clamp(client.CreationProgressStep, 1, 10)));
    }

    public async Task<ClientCreationProgressResult> AdvanceCreationProgressAsync(
        ClientCreationProgressRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenantScopeAccessor.GetAsync(cancellationToken);
        if (tenant is null)
            return new(ClientAdministrationStatus.NotFound, request.ClientId, 0);

        var client = await context.Clients.SingleOrDefaultAsync(
            item => item.Id == request.ClientId
                && (tenant.IsPlatformAdministrator || (item.CompanyId == tenant.CompanyId
                    && context.UserLocations.Any(access =>
                        access.UserId == tenant.UserId && access.LocationId == item.LocationId))),
            cancellationToken);
        if (client is null)
            return new(ClientAdministrationStatus.NotFound, request.ClientId, 0);
        if (!ClientEditorVersion.Matches(client.RowVersion, request.EditorVersion))
            return new(ClientAdministrationStatus.Conflict, request.ClientId, client.CreationProgressStep);
        if (client.Status != ClientStatus.Pending)
            return new(ClientAdministrationStatus.InvalidState, request.ClientId, client.CreationProgressStep);

        try
        {
            var changed = client.AdvanceCreationProgress(request.Step, timeProvider.GetUtcNow());
            if (changed) await context.SaveChangesAsync(cancellationToken);
            return new(
                changed ? ClientAdministrationStatus.Succeeded : ClientAdministrationStatus.NoChanges,
                request.ClientId,
                client.CreationProgressStep,
                ClientEditorVersion.Create(client.RowVersion));
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(ClientAdministrationStatus.Conflict, request.ClientId, client.CreationProgressStep);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return new(ClientAdministrationStatus.InvalidState, request.ClientId, client.CreationProgressStep);
        }
    }

    private async Task<ClientDirectoryDetails> GetRequiredDetailsAsync(
        Guid clientId,
        CancellationToken cancellationToken) =>
        await directory.GetByIdAsync(clientId, cancellationToken)
        ?? throw new InvalidOperationException("The client could not be reloaded after a successful mutation.");

    private static string Normalize(string? value) => (value ?? string.Empty).Trim();

    private static string NormalizeRequired(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim();
    }

    private static ClientAdministrationResult Failed(ClientAdministrationStatus status) =>
        new(status, null, []);
}
