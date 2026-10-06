using CareComply.Domain.Clients;

namespace CareComply.Application.Abstractions.Clients;

public interface IClientGuestInvitationPublicResolver
{
    Task<PublicClientGuestInvitationResult> ResolveAsync(
        string token,
        CancellationToken cancellationToken);

    Task<PublicClientIntakeLocationsResult> ListLocationsAsync(
        string token,
        CancellationToken cancellationToken);

    Task<PublicClientIntakeSupervisorsResult> ListSupervisorsAsync(
        string token,
        Guid locationId,
        CancellationToken cancellationToken);

    Task<PublicClientIntakeCreationResult> CreateBasicDetailsAsync(
        string token,
        ClientBasicDetailsCreateRequest request,
        CancellationToken cancellationToken);

    Task<PublicClientIntakeAdditionalInformationResult> GetAdditionalInformationAsync(
        string token,
        CancellationToken cancellationToken);

    Task<PublicClientIntakeAdditionalInformationResult> UpdateAdditionalInformationAsync(
        string token,
        ClientAdditionalProfileInput additionalProfile,
        string editorVersion,
        CancellationToken cancellationToken);

    Task<PublicClientIntakeContactInformationResult> GetContactInformationAsync(
        string token, CancellationToken cancellationToken);

    Task<PublicClientIntakeContactInformationResult> UpdateContactInformationAsync(
        string token, ClientContactInput contact, IReadOnlyList<ClientLanguageInput> languages,
        string editorVersion, CancellationToken cancellationToken);

    Task<PublicClientIntakeCaregiversResult> ListCaregiversAsync(string token, CancellationToken cancellationToken);
    Task<PublicClientIntakeCaregiverCandidatesResult> ListCaregiverCandidatesAsync(string token, CancellationToken cancellationToken);
    Task<PublicClientIntakeCaregiverMutationResult> CreateCaregiverAsync(string token, ClientCaregiverCreateInput input, CancellationToken cancellationToken);
    Task<PublicClientIntakeCaregiverMutationResult> UpdateCaregiverAsync(string token, Guid assignmentId, ClientCaregiverUpdateInput input, CancellationToken cancellationToken);
    Task<PublicClientIntakeCaregiverMutationResult> EndCaregiverAsync(string token, Guid assignmentId, DateOnly endDate, string editorVersion, CancellationToken cancellationToken);
    Task<PublicClientIntakeCreationResult> ContinueFromCaregiversAsync(string token, CancellationToken cancellationToken);
    Task<PublicClientIntakeContactsResult> ListContactsAsync(string token, CancellationToken cancellationToken);
    Task<PublicClientIntakeContactMutationResult> CreateContactAsync(
        string token, SaveClientContact input, CancellationToken cancellationToken);
    Task<PublicClientIntakeContactMutationResult> UpdateContactAsync(
        string token, Guid contactId, SaveClientContact input, CancellationToken cancellationToken);
    Task<PublicClientIntakeCreationResult> ContinueFromContactsAsync(
        string token, CancellationToken cancellationToken);
    Task<PublicClientIntakeCareCoordinatorsResult> ListCareCoordinatorsAsync(string token, CancellationToken cancellationToken);
    Task<PublicClientIntakeCareCoordinatorMutationResult> CreateCareCoordinatorAsync(string token, SaveCareCoordinator input, CancellationToken cancellationToken);
    Task<PublicClientIntakeCareCoordinatorMutationResult> UpdateCareCoordinatorAsync(string token, Guid coordinatorId, SaveCareCoordinator input, CancellationToken cancellationToken);
    Task<PublicClientIntakeCreationResult> ContinueFromCareCoordinatorsAsync(string token, CancellationToken cancellationToken);
    Task<PublicClientIntakeConsultationProvidersResult> ListConsultationProvidersAsync(string token, CancellationToken cancellationToken);
    Task<PublicClientIntakeConsultationProviderMutationResult> CreateConsultationProviderAsync(string token, SaveConsultationProvider input, CancellationToken cancellationToken);
    Task<PublicClientIntakeConsultationProviderMutationResult> UpdateConsultationProviderAsync(string token, Guid providerId, SaveConsultationProvider input, CancellationToken cancellationToken);
    Task<PublicClientIntakeCreationResult> ContinueFromConsultationProvidersAsync(string token, CancellationToken cancellationToken);
    Task<PublicClientIntakePhysiciansResult> ListPhysiciansAsync(string token, CancellationToken cancellationToken);
    Task<PublicClientIntakePhysicianMutationResult> CreatePhysicianAsync(string token, SavePhysician input, CancellationToken cancellationToken);
    Task<PublicClientIntakePhysicianMutationResult> UpdatePhysicianAsync(string token, Guid physicianId, SavePhysician input, CancellationToken cancellationToken);
    Task<PublicClientIntakeCreationResult> ContinueFromPhysiciansAsync(string token, CancellationToken cancellationToken);
    Task<PublicClientIntakeResponsiblePartiesResult> ListResponsiblePartiesAsync(string token, CancellationToken cancellationToken);
    Task<PublicClientIntakeResponsiblePartyMutationResult> CreateResponsiblePartyAsync(string token, SaveResponsibleParty input, CancellationToken cancellationToken);
    Task<PublicClientIntakeResponsiblePartyMutationResult> UpdateResponsiblePartyAsync(string token, Guid responsiblePartyId, SaveResponsibleParty input, CancellationToken cancellationToken);
    Task<PublicClientIntakeCreationResult> ContinueFromResponsiblePartiesAsync(string token, CancellationToken cancellationToken);
    Task<PublicClientIntakeHospitalizationsResult> ListHospitalizationsAsync(string token, CancellationToken cancellationToken);
    Task<PublicClientIntakeHospitalizationMutationResult> CreateHospitalizationAsync(
        string token, PublicHospitalizationInput input, CancellationToken cancellationToken);
    Task<PublicClientIntakeHospitalizationMutationResult> UpdateHospitalizationAsync(
        string token, Guid hospitalizationId, PublicHospitalizationInput input,
        CancellationToken cancellationToken);
    Task<PublicClientIntakeCompletionResult> CompleteAsync(
        string token, CancellationToken cancellationToken);
}

public sealed record PublicClientGuestInvitationResult(
    PublicClientGuestInvitationState State,
    string? ClientDisplayName = null,
    DateTimeOffset? ExpiresAtUtc = null,
    bool HasCreatedClient = false);

public enum PublicClientGuestInvitationState
{
    Ready,
    Invalid,
    Expired,
    Unavailable,
    Completed,
}

public sealed record PublicClientIntakeLocation(Guid Id, string DisplayName);

public sealed record PublicClientIntakeLocationsResult(
    PublicClientGuestInvitationState State,
    IReadOnlyList<PublicClientIntakeLocation> Locations);

public sealed record PublicClientIntakeSupervisor(Guid Id, string DisplayName, string JobTitle);

public sealed record PublicClientIntakeSupervisorsResult(
    PublicClientGuestInvitationState State,
    IReadOnlyList<PublicClientIntakeSupervisor> Supervisors);

public sealed record PublicClientIntakeCreationResult(
    PublicClientIntakeCreationStatus Status,
    int? NextStep = null,
    string? EditorVersion = null,
    bool HasCreatedClient = false);

public enum PublicClientIntakeCreationStatus
{
    Created,
    AlreadyStarted,
    Invalid,
    Expired,
    Unavailable,
    Completed,
    DuplicatePmi,
    InvalidAssignment,
    Conflict,
}

public sealed record PublicClientIntakeAdditionalInformationResult(
    PublicClientIntakeCreationStatus Status,
    ClientAdditionalProfileInput? AdditionalInformation = null,
    string? EditorVersion = null,
    int? CreationProgressStep = null);

public sealed record PublicClientIntakeContactInformationResult(
    PublicClientIntakeCreationStatus Status,
    ClientContactInput? Contact = null,
    IReadOnlyList<ClientLanguageInput>? Languages = null,
    string? EditorVersion = null,
    int? CreationProgressStep = null);

public sealed record PublicClientIntakeCaregiverCandidate(Guid Id, string DisplayName, string JobTitle);
public sealed record PublicClientIntakeCaregiverCandidatesResult(
    PublicClientIntakeCreationStatus Status,
    IReadOnlyList<PublicClientIntakeCaregiverCandidate>? Candidates = null);
public sealed record PublicClientIntakeCaregiversResult(
    PublicClientIntakeCreationStatus Status,
    IReadOnlyList<ClientCaregiverItem>? Caregivers = null,
    IReadOnlyList<string>? Services = null,
    int? CreationProgressStep = null);
public sealed record ClientCaregiverCreateInput(
    Guid CaregiverUserId, ClientCaregiverType Type, bool ClientPreference, string? Services,
    DateOnly StartDate, DateOnly? EndDate, string? Notes, string? RelationshipToClient,
    bool? LivesWithClient);
public sealed record ClientCaregiverUpdateInput(
    ClientCaregiverType Type, bool ClientPreference, string? Services, DateOnly StartDate,
    DateOnly? EndDate, string? Notes, string EditorVersion, string? RelationshipToClient,
    bool? LivesWithClient);
public sealed record PublicClientIntakeCaregiverMutationResult(
    PublicClientIntakeCreationStatus Status,
    ClientCaregiverMutation? Mutation = null);
public sealed record PublicClientIntakeContactsResult(
    PublicClientIntakeCreationStatus Status,
    IReadOnlyList<ClientContactItem>? Contacts = null,
    int? CreationProgressStep = null);
public sealed record PublicClientIntakeContactMutationResult(
    PublicClientIntakeCreationStatus Status,
    ClientContactItem? Contact = null);
public sealed record PublicClientIntakeCareCoordinatorsResult(
    PublicClientIntakeCreationStatus Status,
    IReadOnlyList<CareCoordinatorItem>? Coordinators = null,
    int? CreationProgressStep = null);
public sealed record PublicClientIntakeCareCoordinatorMutationResult(
    PublicClientIntakeCreationStatus Status,
    CareCoordinatorItem? Coordinator = null);
public sealed record PublicClientIntakeConsultationProvidersResult(
    PublicClientIntakeCreationStatus Status,
    IReadOnlyList<ClinicalContactItem>? Providers = null,
    int? CreationProgressStep = null);
public sealed record PublicClientIntakeConsultationProviderMutationResult(
    PublicClientIntakeCreationStatus Status,
    ClinicalContactItem? Provider = null);
public sealed record PublicClientIntakePhysiciansResult(
    PublicClientIntakeCreationStatus Status,
    IReadOnlyList<ClinicalContactItem>? Physicians = null,
    int? CreationProgressStep = null);
public sealed record PublicClientIntakePhysicianMutationResult(
    PublicClientIntakeCreationStatus Status,
    ClinicalContactItem? Physician = null);
public sealed record PublicClientIntakeResponsiblePartiesResult(
    PublicClientIntakeCreationStatus Status,
    IReadOnlyList<ResponsiblePartyItem>? ResponsibleParties = null,
    int? CreationProgressStep = null);
public sealed record PublicClientIntakeResponsiblePartyMutationResult(
    PublicClientIntakeCreationStatus Status,
    ResponsiblePartyItem? ResponsibleParty = null);
public sealed record PublicHospitalizationInput(string HospitalName, DateTimeOffset AdmittedAtUtc,
    DateTimeOffset? DischargedAtUtc, string Reason, string? Diagnosis,
    string? DischargeInstructions, string? EditorVersion);
public sealed record PublicClientIntakeHospitalizationsResult(
    PublicClientIntakeCreationStatus Status,
    IReadOnlyList<HospitalizationItem>? Hospitalizations = null,
    int? CreationProgressStep = null);
public sealed record PublicClientIntakeHospitalizationMutationResult(
    PublicClientIntakeCreationStatus Status,
    HospitalizationMutation? Mutation = null);
public sealed record PublicClientIntakeCompletionResult(
    PublicClientIntakeCreationStatus Status,
    DateTimeOffset? CompletedAtUtc = null);
