using CareComply.Application.Abstractions.Clients;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareComply.Infrastructure.Clients;

internal sealed class EfClientGuestInvitationPublicResolver(
    CareComplyDbContext context,
    ClientBasicDetailsPersistence basicDetailsPersistence,
    ClientContactInformationPersistence contactInformationPersistence,
    IClientCaregiverStore caregiverStore,
    IClientContactStore contactStore,
    IClientCareCoordinatorStore careCoordinatorStore,
    IClientClinicalContactStore clinicalContactStore,
    IClientResponsiblePartyStore responsiblePartyStore,
    IClientHospitalizationStore hospitalizationStore,
    TimeProvider timeProvider,
    IOptions<ClientGuestInvitationOptions> options) : IClientGuestInvitationPublicResolver
{
    public async Task<PublicClientGuestInvitationResult> ResolveAsync(string token, CancellationToken cancellationToken)
    {
        var invitation = await FindInvitationAsync(token, false, cancellationToken);
        if (invitation is null) return new(PublicClientGuestInvitationState.Invalid);
        var state = LifecycleState(invitation);
        if (state != PublicClientGuestInvitationState.Ready) return new(state);
        if (invitation.OpenedAtUtc is null)
        {
            await context.ClientGuestInvitations
                .Where(value => value.Id == invitation.Id && value.OpenedAtUtc == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(value => value.OpenedAtUtc, timeProvider.GetUtcNow()),
                    cancellationToken);
        }
        return new(state, invitation.ClientName, invitation.ExpiresAtUtc, invitation.CreatedClientId is not null);
    }

    public async Task<PublicClientIntakeLocationsResult> ListLocationsAsync(
        string token, CancellationToken cancellationToken)
    {
        var invitation = await FindInvitationAsync(token, false, cancellationToken);
        var state = invitation is null ? PublicClientGuestInvitationState.Invalid : LifecycleState(invitation);
        if (state != PublicClientGuestInvitationState.Ready || invitation is null) return new(state, []);
        var locations = await context.CompanyLocations.AsNoTracking()
            .Where(value => value.CompanyId == invitation.CompanyId && value.IsActive)
            .OrderByDescending(value => value.IsMain).ThenBy(value => value.Name)
            .Select(value => new PublicClientIntakeLocation(value.Id, value.Name))
            .ToArrayAsync(cancellationToken);
        return new(state, locations);
    }

    public async Task<PublicClientIntakeSupervisorsResult> ListSupervisorsAsync(
        string token, Guid locationId, CancellationToken cancellationToken)
    {
        var invitation = await FindInvitationAsync(token, false, cancellationToken);
        var state = invitation is null ? PublicClientGuestInvitationState.Invalid : LifecycleState(invitation);
        if (state != PublicClientGuestInvitationState.Ready || invitation is null) return new(state, []);
        if (!await context.CompanyLocations.AsNoTracking().AnyAsync(value =>
                value.Id == locationId && value.CompanyId == invitation.CompanyId && value.IsActive,
                cancellationToken))
            return new(PublicClientGuestInvitationState.Invalid, []);
        var supervisors = await context.Users.AsNoTracking()
            .Where(user => user.CompanyId == invitation.CompanyId && user.IsEmployee && user.IsActive
                && context.UserLocations.Any(access => access.UserId == user.Id && access.LocationId == locationId))
            .OrderBy(user => user.LastName).ThenBy(user => user.FirstName)
            .Select(user => new PublicClientIntakeSupervisor(
                user.Id, user.FirstName + " " + user.LastName, user.JobTitle))
            .ToArrayAsync(cancellationToken);
        return new(state, supervisors);
    }

    public async Task<PublicClientIntakeCreationResult> CreateBasicDetailsAsync(
        string token, ClientBasicDetailsCreateRequest request, CancellationToken cancellationToken)
    {
        var invitation = await FindInvitationAsync(token, true, cancellationToken);
        if (invitation is null) return new(PublicClientIntakeCreationStatus.Invalid);
        var lifecycle = LifecycleState(invitation);
        if (lifecycle != PublicClientGuestInvitationState.Ready) return new(MapLifecycle(lifecycle));
        if (invitation.CreatedClientId is { } existingId)
            return await ExistingContinuationAsync(existingId, cancellationToken);
        try
        {
            var preparation = await basicDetailsPersistence.PrepareAsync(
                invitation.CompanyId, request, cancellationToken);
            if (preparation.Client is null)
                return new(preparation.Status == ClientAdministrationStatus.DuplicatePmi
                    ? PublicClientIntakeCreationStatus.DuplicatePmi
                    : PublicClientIntakeCreationStatus.InvalidAssignment);
            invitation.BindCreatedClient(preparation.Client.Id, timeProvider.GetUtcNow());
            await context.SaveChangesAsync(cancellationToken);
            return new(
                PublicClientIntakeCreationStatus.Created,
                preparation.Client.CreationProgressStep,
                ClientEditorVersion.Create(preparation.Client.RowVersion),
                true);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            var current = await FindInvitationAsync(token, false, cancellationToken);
            return current?.CreatedClientId is { } currentId
                ? await ExistingContinuationAsync(currentId, cancellationToken)
                : new(PublicClientIntakeCreationStatus.Conflict);
        }
        catch (DbUpdateException exception) when (ClientPersistenceErrorClassifier.IsDuplicatePmi(exception))
        {
            context.ChangeTracker.Clear();
            var current = await FindInvitationAsync(token, false, cancellationToken);
            return current?.CreatedClientId is { } currentId
                ? await ExistingContinuationAsync(currentId, cancellationToken)
                : new(PublicClientIntakeCreationStatus.DuplicatePmi);
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();
            var current = await FindInvitationAsync(token, false, cancellationToken);
            return current?.CreatedClientId is { } currentId
                ? await ExistingContinuationAsync(currentId, cancellationToken)
                : new(PublicClientIntakeCreationStatus.Conflict);
        }
        catch (ArgumentException)
        {
            return new(PublicClientIntakeCreationStatus.InvalidAssignment);
        }
    }

    public async Task<PublicClientIntakeAdditionalInformationResult> GetAdditionalInformationAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var resolution = await ResolvePendingClientAsync(token, false, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
        {
            return new(resolution.Status);
        }

        var profile = await context.ClientAdditionalProfiles.AsNoTracking()
            .SingleOrDefaultAsync(value => value.ClientId == resolution.Client.Id, cancellationToken);
        return new(
            PublicClientIntakeCreationStatus.Created,
            ToInput(profile),
            ClientEditorVersion.Create(resolution.Client.RowVersion),
            resolution.Client.CreationProgressStep);
    }

    public async Task<PublicClientIntakeAdditionalInformationResult> UpdateAdditionalInformationAsync(
        string token,
        ClientAdditionalProfileInput additionalProfile,
        string editorVersion,
        CancellationToken cancellationToken)
    {
        var resolution = await ResolvePendingClientAsync(token, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
        {
            return new(resolution.Status);
        }

        var status = await ClientAdditionalInformationPersistence.ApplyAsync(
            context,
            resolution.Client,
            additionalProfile,
            editorVersion,
            timeProvider.GetUtcNow(),
            cancellationToken);
        return status switch
        {
            ClientAdministrationStatus.Succeeded or ClientAdministrationStatus.NoChanges => new(
                PublicClientIntakeCreationStatus.Created,
                additionalProfile,
                ClientEditorVersion.Create(resolution.Client.RowVersion),
                resolution.Client.CreationProgressStep),
            ClientAdministrationStatus.Conflict => new(PublicClientIntakeCreationStatus.Conflict),
            _ => new(PublicClientIntakeCreationStatus.InvalidAssignment),
        };
    }

    public async Task<PublicClientIntakeContactInformationResult> GetContactInformationAsync(
        string token, CancellationToken cancellationToken)
    {
        var invitation = await FindInvitationAsync(token, false, cancellationToken);
        var resolution = await ResolvePendingClientAsync(token, false, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created
            || resolution.Client is null || invitation is null)
            return new(resolution.Status);
        var client = resolution.Client;
        if (client.CreationProgressStep < 3)
            return new(PublicClientIntakeCreationStatus.InvalidAssignment);
        var languages = await context.ClientLanguagePreferences.AsNoTracking()
            .Where(value => value.ClientId == client.Id)
            .OrderByDescending(value => value.IsPrimary).ThenBy(value => value.LanguageName)
            .Select(value => new ClientLanguageInput(value.LanguageCode, value.LanguageName, value.IsPrimary))
            .ToArrayAsync(cancellationToken);
        return new(PublicClientIntakeCreationStatus.Created,
            new ClientContactInput(null,
                string.IsNullOrWhiteSpace(client.Email) ? invitation.Email : client.Email,
                string.IsNullOrWhiteSpace(client.PrimaryPhone) ? invitation.MobileNumber : client.PrimaryPhone,
                client.SecondaryPhone, client.AddressLine1, client.AddressLine2, client.City,
                client.State, client.PostalCode, client.County, client.Country),
            languages, ClientEditorVersion.Create(client.RowVersion), client.CreationProgressStep);
    }

    public async Task<PublicClientIntakeContactInformationResult> UpdateContactInformationAsync(
        string token, ClientContactInput contact, IReadOnlyList<ClientLanguageInput> languages,
        string editorVersion, CancellationToken cancellationToken)
    {
        var resolution = await ResolvePendingClientAsync(token, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        if (resolution.Client.CreationProgressStep < 3)
            return new(PublicClientIntakeCreationStatus.InvalidAssignment);
        var persistence = await contactInformationPersistence.ApplyAsync(
            resolution.Client, contact, languages, editorVersion, timeProvider.GetUtcNow(), cancellationToken);
        return persistence.Status switch
        {
            ClientAdministrationStatus.Succeeded or ClientAdministrationStatus.NoChanges => new(
                PublicClientIntakeCreationStatus.Created, contact, languages,
                ClientEditorVersion.Create(resolution.Client.RowVersion), resolution.Client.CreationProgressStep),
            ClientAdministrationStatus.Conflict => new(PublicClientIntakeCreationStatus.Conflict),
            _ => new(PublicClientIntakeCreationStatus.InvalidAssignment),
        };
    }

    public async Task<PublicClientIntakeCaregiverCandidatesResult> ListCaregiverCandidatesAsync(
        string token, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStep4ClientAsync(token, false, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        var values = await caregiverStore.CandidatesForClientAsync(resolution.Client, cancellationToken);
        return new(PublicClientIntakeCreationStatus.Created, values.Select(value =>
            new PublicClientIntakeCaregiverCandidate(value.Id, value.Name, value.Title)).ToArray());
    }

    public async Task<PublicClientIntakeCaregiversResult> ListCaregiversAsync(
        string token, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStep4ClientAsync(token, false, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        var assignments = await caregiverStore.ListForClientAsync(resolution.Client, cancellationToken);
        var services = await context.ClientServices.AsNoTracking()
            .Where(value => value.ClientId == resolution.Client.Id && value.IsActive)
            .OrderBy(value => value.Name).Select(value => value.Name).ToArrayAsync(cancellationToken);
        return new(PublicClientIntakeCreationStatus.Created, assignments.Items, services,
            resolution.Client.CreationProgressStep);
    }

    public async Task<PublicClientIntakeCaregiverMutationResult> CreateCaregiverAsync(
        string token, ClientCaregiverCreateInput input, CancellationToken cancellationToken)
    {
        var invitation = await FindInvitationAsync(token, false, cancellationToken);
        var resolution = await ResolveStep4ClientAsync(token, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created
            || resolution.Client is null || invitation is null)
            return new(resolution.Status);
        var mutation = await caregiverStore.CreateForClientAsync(resolution.Client,
            new(resolution.Client.Id, input.CaregiverUserId, input.Type, input.ClientPreference,
                input.Services, input.StartDate, input.EndDate, input.Notes, null,
                timeProvider.GetUtcNow(), input.RelationshipToClient, input.LivesWithClient,
                invitation.Id), cancellationToken);
        return new(PublicClientIntakeCreationStatus.Created, mutation);
    }

    public async Task<PublicClientIntakeCaregiverMutationResult> UpdateCaregiverAsync(
        string token, Guid assignmentId, ClientCaregiverUpdateInput input,
        CancellationToken cancellationToken)
    {
        var resolution = await ResolveStep4ClientAsync(token, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        var mutation = await caregiverStore.UpdateForClientAsync(resolution.Client,
            new(resolution.Client.Id, assignmentId, input.Type, input.ClientPreference,
                input.Services, input.StartDate, input.EndDate, input.Notes, input.EditorVersion,
                timeProvider.GetUtcNow(), input.RelationshipToClient, input.LivesWithClient),
            cancellationToken);
        return new(PublicClientIntakeCreationStatus.Created, mutation);
    }

    public async Task<PublicClientIntakeCaregiverMutationResult> EndCaregiverAsync(
        string token, Guid assignmentId, DateOnly endDate, string editorVersion,
        CancellationToken cancellationToken)
    {
        var resolution = await ResolveStep4ClientAsync(token, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        var mutation = await caregiverStore.EndForClientAsync(resolution.Client,
            new(resolution.Client.Id, assignmentId, endDate, editorVersion, timeProvider.GetUtcNow()),
            cancellationToken);
        return new(PublicClientIntakeCreationStatus.Created, mutation);
    }

    public async Task<PublicClientIntakeCreationResult> ContinueFromCaregiversAsync(
        string token, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStep4ClientAsync(token, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        try
        {
            resolution.Client.AdvanceCreationProgress(5, timeProvider.GetUtcNow());
            await context.SaveChangesAsync(cancellationToken);
            return new(PublicClientIntakeCreationStatus.Created, 5,
                ClientEditorVersion.Create(resolution.Client.RowVersion), true);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(PublicClientIntakeCreationStatus.Conflict);
        }
        catch (InvalidOperationException)
        {
            return new(PublicClientIntakeCreationStatus.InvalidAssignment);
        }
    }

    public async Task<PublicClientIntakeContactsResult> ListContactsAsync(
        string token, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 5, false, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        return new(PublicClientIntakeCreationStatus.Created,
            await contactStore.ListForClientAsync(resolution.Client, cancellationToken),
            resolution.Client.CreationProgressStep);
    }

    public async Task<PublicClientIntakeContactMutationResult> CreateContactAsync(
        string token, SaveClientContact input, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 5, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        return new(PublicClientIntakeCreationStatus.Created,
            await contactStore.CreateForClientAsync(resolution.Client, input, cancellationToken));
    }

    public async Task<PublicClientIntakeContactMutationResult> UpdateContactAsync(
        string token, Guid contactId, SaveClientContact input, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 5, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        return new(PublicClientIntakeCreationStatus.Created,
            await contactStore.UpdateForClientAsync(resolution.Client, contactId, input, cancellationToken));
    }

    public async Task<PublicClientIntakeCreationResult> ContinueFromContactsAsync(
        string token, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 5, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        try
        {
            resolution.Client.AdvanceCreationProgress(6, timeProvider.GetUtcNow());
            await context.SaveChangesAsync(cancellationToken);
            return new(PublicClientIntakeCreationStatus.Created, 6,
                ClientEditorVersion.Create(resolution.Client.RowVersion), true);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(PublicClientIntakeCreationStatus.Conflict);
        }
        catch (InvalidOperationException)
        {
            return new(PublicClientIntakeCreationStatus.InvalidAssignment);
        }
    }

    public async Task<PublicClientIntakeCareCoordinatorsResult> ListCareCoordinatorsAsync(string token, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 6, false, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null) return new(resolution.Status);
        return new(PublicClientIntakeCreationStatus.Created,
            await careCoordinatorStore.ListForClientAsync(resolution.Client, cancellationToken),
            resolution.Client.CreationProgressStep);
    }

    public async Task<PublicClientIntakeCareCoordinatorMutationResult> CreateCareCoordinatorAsync(
        string token, SaveCareCoordinator input, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 6, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null) return new(resolution.Status);
        return new(PublicClientIntakeCreationStatus.Created,
            await careCoordinatorStore.CreateForClientAsync(resolution.Client, input, cancellationToken));
    }

    public async Task<PublicClientIntakeCareCoordinatorMutationResult> UpdateCareCoordinatorAsync(
        string token, Guid coordinatorId, SaveCareCoordinator input, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 6, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null) return new(resolution.Status);
        return new(PublicClientIntakeCreationStatus.Created,
            await careCoordinatorStore.UpdateForClientAsync(resolution.Client, coordinatorId, input, cancellationToken));
    }

    public async Task<PublicClientIntakeCreationResult> ContinueFromCareCoordinatorsAsync(string token, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 6, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null) return new(resolution.Status);
        try
        {
            resolution.Client.AdvanceCreationProgress(7, timeProvider.GetUtcNow());
            await context.SaveChangesAsync(cancellationToken);
            return new(PublicClientIntakeCreationStatus.Created, 7,
                ClientEditorVersion.Create(resolution.Client.RowVersion), true);
        }
        catch (DbUpdateConcurrencyException) { return new(PublicClientIntakeCreationStatus.Conflict); }
        catch (InvalidOperationException) { return new(PublicClientIntakeCreationStatus.InvalidAssignment); }
    }

    public async Task<PublicClientIntakeConsultationProvidersResult> ListConsultationProvidersAsync(
        string token, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 7, false, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        return new(PublicClientIntakeCreationStatus.Created,
            await clinicalContactStore.ListForClientAsync(
                resolution.Client, ClientContactType.ConsultationProvider, cancellationToken),
            resolution.Client.CreationProgressStep);
    }

    public async Task<PublicClientIntakeConsultationProviderMutationResult> CreateConsultationProviderAsync(
        string token, SaveConsultationProvider input, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 7, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        return new(PublicClientIntakeCreationStatus.Created,
            await clinicalContactStore.CreateConsultationProviderForClientAsync(
                resolution.Client, input, cancellationToken));
    }

    public async Task<PublicClientIntakeConsultationProviderMutationResult> UpdateConsultationProviderAsync(
        string token, Guid providerId, SaveConsultationProvider input, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 7, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        return new(PublicClientIntakeCreationStatus.Created,
            await clinicalContactStore.UpdateConsultationProviderForClientAsync(
                resolution.Client, providerId, input, cancellationToken));
    }

    public async Task<PublicClientIntakeCreationResult> ContinueFromConsultationProvidersAsync(
        string token, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 7, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        try
        {
            resolution.Client.AdvanceCreationProgress(8, timeProvider.GetUtcNow());
            await context.SaveChangesAsync(cancellationToken);
            return new(PublicClientIntakeCreationStatus.Created, 8,
                ClientEditorVersion.Create(resolution.Client.RowVersion), true);
        }
        catch (DbUpdateConcurrencyException) { return new(PublicClientIntakeCreationStatus.Conflict); }
        catch (InvalidOperationException) { return new(PublicClientIntakeCreationStatus.InvalidAssignment); }
    }

    public async Task<PublicClientIntakePhysiciansResult> ListPhysiciansAsync(
        string token, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 8, false, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        return new(PublicClientIntakeCreationStatus.Created,
            await clinicalContactStore.ListForClientAsync(
                resolution.Client, ClientContactType.Physician, cancellationToken),
            resolution.Client.CreationProgressStep);
    }

    public async Task<PublicClientIntakePhysicianMutationResult> CreatePhysicianAsync(
        string token, SavePhysician input, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 8, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        return new(PublicClientIntakeCreationStatus.Created,
            await clinicalContactStore.CreatePhysicianForClientAsync(
                resolution.Client, input, cancellationToken));
    }

    public async Task<PublicClientIntakePhysicianMutationResult> UpdatePhysicianAsync(
        string token, Guid physicianId, SavePhysician input, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 8, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        return new(PublicClientIntakeCreationStatus.Created,
            await clinicalContactStore.UpdatePhysicianForClientAsync(
                resolution.Client, physicianId, input, cancellationToken));
    }

    public async Task<PublicClientIntakeCreationResult> ContinueFromPhysiciansAsync(
        string token, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 8, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        try
        {
            resolution.Client.AdvanceCreationProgress(9, timeProvider.GetUtcNow());
            await context.SaveChangesAsync(cancellationToken);
            return new(PublicClientIntakeCreationStatus.Created, 9,
                ClientEditorVersion.Create(resolution.Client.RowVersion), true);
        }
        catch (DbUpdateConcurrencyException) { return new(PublicClientIntakeCreationStatus.Conflict); }
        catch (InvalidOperationException) { return new(PublicClientIntakeCreationStatus.InvalidAssignment); }
    }

    public async Task<PublicClientIntakeResponsiblePartiesResult> ListResponsiblePartiesAsync(
        string token, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 9, false, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        return new(PublicClientIntakeCreationStatus.Created,
            await responsiblePartyStore.ListForClientAsync(resolution.Client, cancellationToken),
            resolution.Client.CreationProgressStep);
    }

    public async Task<PublicClientIntakeResponsiblePartyMutationResult> CreateResponsiblePartyAsync(
        string token, SaveResponsibleParty input, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 9, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        return new(PublicClientIntakeCreationStatus.Created,
            await responsiblePartyStore.CreateForClientAsync(
                resolution.Client, input, cancellationToken));
    }

    public async Task<PublicClientIntakeResponsiblePartyMutationResult> UpdateResponsiblePartyAsync(
        string token, Guid responsiblePartyId, SaveResponsibleParty input,
        CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 9, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        return new(PublicClientIntakeCreationStatus.Created,
            await responsiblePartyStore.UpdateForClientAsync(
                resolution.Client, responsiblePartyId, input, cancellationToken));
    }

    public async Task<PublicClientIntakeCreationResult> ContinueFromResponsiblePartiesAsync(
        string token, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 9, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        try
        {
            resolution.Client.AdvanceCreationProgress(10, timeProvider.GetUtcNow());
            await context.SaveChangesAsync(cancellationToken);
            return new(PublicClientIntakeCreationStatus.Created, 10,
                ClientEditorVersion.Create(resolution.Client.RowVersion), true);
        }
        catch (DbUpdateConcurrencyException) { return new(PublicClientIntakeCreationStatus.Conflict); }
        catch (InvalidOperationException) { return new(PublicClientIntakeCreationStatus.InvalidAssignment); }
    }

    public async Task<PublicClientIntakeHospitalizationsResult> ListHospitalizationsAsync(
        string token, CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 10, false, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        var rows = await hospitalizationStore.ListForClient(resolution.Client, cancellationToken);
        return new(PublicClientIntakeCreationStatus.Created, rows.Items,
            resolution.Client.CreationProgressStep);
    }

    public async Task<PublicClientIntakeHospitalizationMutationResult> CreateHospitalizationAsync(
        string token, PublicHospitalizationInput input, CancellationToken cancellationToken)
    {
        var invitation = await FindInvitationAsync(token, false, cancellationToken);
        var resolution = await ResolveStepClientAsync(token, 10, false, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created
            || resolution.Client is null || invitation is null)
            return new(resolution.Status);
        var mutation = await hospitalizationStore.CreateForClient(resolution.Client, new(
            resolution.Client.Id, input.HospitalName, input.AdmittedAtUtc, input.DischargedAtUtc,
            input.Reason, input.Diagnosis, input.DischargeInstructions, null,
            timeProvider.GetUtcNow(), invitation.Id), cancellationToken);
        return new(PublicClientIntakeCreationStatus.Created, mutation);
    }

    public async Task<PublicClientIntakeHospitalizationMutationResult> UpdateHospitalizationAsync(
        string token, Guid hospitalizationId, PublicHospitalizationInput input,
        CancellationToken cancellationToken)
    {
        var resolution = await ResolveStepClientAsync(token, 10, true, cancellationToken);
        if (resolution.Status != PublicClientIntakeCreationStatus.Created || resolution.Client is null)
            return new(resolution.Status);
        var mutation = await hospitalizationStore.UpdateForClient(resolution.Client, new(
            resolution.Client.Id, hospitalizationId, input.HospitalName, input.AdmittedAtUtc,
            input.DischargedAtUtc, input.Reason, input.Diagnosis, input.DischargeInstructions,
            input.EditorVersion ?? string.Empty, timeProvider.GetUtcNow()), cancellationToken);
        return new(PublicClientIntakeCreationStatus.Created, mutation);
    }

    public async Task<PublicClientIntakeCompletionResult> CompleteAsync(
        string token, CancellationToken cancellationToken)
    {
        if (!ClientGuestInvitationToken.TryHash(token, out var tokenHash))
            return new(PublicClientIntakeCreationStatus.Invalid);

        var strategy = context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<PublicClientIntakeCompletionResult>(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var session = await context.ClientGuestSessions.SingleOrDefaultAsync(
                value => value.SessionTokenHash.SequenceEqual(tokenHash), cancellationToken);
            if (session is null) return new(PublicClientIntakeCreationStatus.Invalid);
            var currentTime = timeProvider.GetUtcNow();
            if (session.RevokedAtUtc is not null || session.CompletedAtUtc is not null
                || currentTime >= session.ExpiresAtUtc
                || currentTime - session.LastActivityAtUtc >= options.Value.GuestSessionInactivityTimeout)
                return new(PublicClientIntakeCreationStatus.Invalid);
            var invitation = await context.ClientGuestInvitations.SingleOrDefaultAsync(
                value => value.Id == session.ClientGuestInvitationId, cancellationToken);
            if (invitation is null) return new(PublicClientIntakeCreationStatus.Invalid);
            if (invitation.CompletedAtUtc is { } completedAtUtc)
                return new(PublicClientIntakeCreationStatus.Completed, completedAtUtc);

            var now = new DateTimeOffset(
                currentTime.Ticks - currentTime.Ticks % TimeSpan.TicksPerSecond,
                TimeSpan.Zero);
            if (invitation.RevokedAtUtc is not null)
                return new(PublicClientIntakeCreationStatus.Unavailable);
            if (now >= invitation.ExpiresAtUtc)
                return new(PublicClientIntakeCreationStatus.Expired);
            if (invitation.CreatedClientId is not { } clientId)
                return new(PublicClientIntakeCreationStatus.InvalidAssignment);

            var client = await context.Clients.SingleOrDefaultAsync(value =>
                value.Id == clientId && value.CompanyId == invitation.CompanyId,
                cancellationToken);
            if (client is null || client.Status != ClientStatus.Pending
                || client.CreationProgressStep < 10)
                return new(PublicClientIntakeCreationStatus.InvalidAssignment);

            try
            {
                client.ChangeStatus(ClientStatus.Active, null, null, now);
                invitation.Complete(now);
                session.Complete(now);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new(PublicClientIntakeCreationStatus.Created, now);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                context.ChangeTracker.Clear();
                var completed = await context.ClientGuestInvitations.AsNoTracking()
                    .Where(value => value.Id == session.ClientGuestInvitationId)
                    .Select(value => value.CompletedAtUtc)
                    .SingleOrDefaultAsync(cancellationToken);
                return completed is { } authoritativeTimestamp
                    ? new(PublicClientIntakeCreationStatus.Completed, authoritativeTimestamp)
                    : new(PublicClientIntakeCreationStatus.Conflict);
            }
            catch (ArgumentException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(PublicClientIntakeCreationStatus.InvalidAssignment);
            }
        });
    }

    private async Task<PublicClientIntakeCreationResult> ExistingContinuationAsync(
        Guid clientId, CancellationToken cancellationToken)
    {
        var client = await context.Clients.AsNoTracking()
            .Where(value => value.Id == clientId)
            .Select(value => new { value.CreationProgressStep, value.RowVersion })
            .SingleOrDefaultAsync(cancellationToken);
        return client is null
            ? new(PublicClientIntakeCreationStatus.Conflict)
            : new(PublicClientIntakeCreationStatus.AlreadyStarted, client.CreationProgressStep,
                ClientEditorVersion.Create(client.RowVersion), true);
    }

    private async Task<ClientGuestInvitation?> FindInvitationAsync(
        string token, bool tracking, CancellationToken cancellationToken)
    {
        if (!ClientGuestInvitationToken.TryHash(token, out var tokenHash)) return null;
        var session = await context.ClientGuestSessions.AsNoTracking().SingleOrDefaultAsync(
            value => value.SessionTokenHash.SequenceEqual(tokenHash), cancellationToken);
        if (session is null) return null;
        var now = timeProvider.GetUtcNow();
        if (session.RevokedAtUtc is not null || session.CompletedAtUtc is not null
            || now >= session.ExpiresAtUtc
            || now - session.LastActivityAtUtc >= options.Value.GuestSessionInactivityTimeout)
            return null;
        await context.ClientGuestSessions
            .Where(value => value.Id == session.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(value => value.LastActivityAtUtc, now),
                cancellationToken);
        var query = context.ClientGuestInvitations.Where(value => value.Id == session.ClientGuestInvitationId);
        return await (tracking ? query : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<(PublicClientIntakeCreationStatus Status, Client? Client)> ResolvePendingClientAsync(
        string token,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var invitation = await FindInvitationAsync(token, false, cancellationToken);
        if (invitation is null)
        {
            return (PublicClientIntakeCreationStatus.Invalid, null);
        }

        var lifecycle = LifecycleState(invitation);
        if (lifecycle != PublicClientGuestInvitationState.Ready)
        {
            return (MapLifecycle(lifecycle), null);
        }

        if (invitation.CreatedClientId is not { } clientId)
        {
            return (PublicClientIntakeCreationStatus.InvalidAssignment, null);
        }

        var query = context.Clients.Where(value =>
            value.Id == clientId
            && value.CompanyId == invitation.CompanyId
            && value.Status == ClientStatus.Pending);
        var client = await (tracking ? query : query.AsNoTracking()).SingleOrDefaultAsync(cancellationToken);
        return client is null
            ? (PublicClientIntakeCreationStatus.InvalidAssignment, null)
            : (PublicClientIntakeCreationStatus.Created, client);
    }

    private async Task<(PublicClientIntakeCreationStatus Status, Client? Client)> ResolveStep4ClientAsync(
        string token, bool tracking, CancellationToken cancellationToken)
    {
        var resolution = await ResolvePendingClientAsync(token, tracking, cancellationToken);
        return resolution.Status == PublicClientIntakeCreationStatus.Created
            && resolution.Client is { CreationProgressStep: >= 4 }
            ? resolution
            : (resolution.Status == PublicClientIntakeCreationStatus.Created
                ? PublicClientIntakeCreationStatus.InvalidAssignment
                : resolution.Status, null);
    }

    private async Task<(PublicClientIntakeCreationStatus Status, Client? Client)> ResolveStepClientAsync(
        string token, int minimumStep, bool tracking, CancellationToken cancellationToken)
    {
        var resolution = await ResolvePendingClientAsync(token, tracking, cancellationToken);
        return resolution.Status == PublicClientIntakeCreationStatus.Created
            && resolution.Client is { } client && client.CreationProgressStep >= minimumStep
            ? resolution
            : (resolution.Status == PublicClientIntakeCreationStatus.Created
                ? PublicClientIntakeCreationStatus.InvalidAssignment
                : resolution.Status, null);
    }

    private static ClientAdditionalProfileInput ToInput(ClientAdditionalProfile? profile) => new(
        profile?.ProgramType ?? ClientProgramType.None,
        profile?.ReferralDetails,
        profile?.HomeCareRating,
        profile?.MedicalEquipment,
        profile?.PcaComplexity,
        profile?.Demographics,
        profile?.Smokes,
        profile?.IsVeteran,
        profile?.HasPets,
        profile?.LivingArrangement,
        profile?.PreferredClinic,
        profile?.PreferredHospital,
        profile?.PreferredAmbulance,
        profile?.CaregiverPreference,
        profile?.NutritionalRequirements,
        profile?.Diet,
        profile?.MealSchedule,
        profile?.Notes,
        profile?.AdditionalInformation);

    private PublicClientGuestInvitationState LifecycleState(ClientGuestInvitation invitation)
    {
        if (invitation.CompletedAtUtc is not null) return PublicClientGuestInvitationState.Completed;
        if (invitation.RevokedAtUtc is not null) return PublicClientGuestInvitationState.Unavailable;
        return timeProvider.GetUtcNow() >= invitation.ExpiresAtUtc
            ? PublicClientGuestInvitationState.Expired
            : PublicClientGuestInvitationState.Ready;
    }

    private static PublicClientIntakeCreationStatus MapLifecycle(PublicClientGuestInvitationState state) => state switch
    {
        PublicClientGuestInvitationState.Expired => PublicClientIntakeCreationStatus.Expired,
        PublicClientGuestInvitationState.Unavailable => PublicClientIntakeCreationStatus.Unavailable,
        PublicClientGuestInvitationState.Completed => PublicClientIntakeCreationStatus.Completed,
        _ => PublicClientIntakeCreationStatus.Invalid,
    };
}
