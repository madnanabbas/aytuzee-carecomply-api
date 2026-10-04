namespace CareComply.Api.Contracts.PublicClientIntake;

using CareComply.Api.Contracts.Clients;

public sealed record PublicClientInvitationResponse(
    string State,
    string? ClientDisplayName,
    DateTimeOffset? ExpiresAtUtc,
    bool HasCreatedClient);

public sealed record PublicClientGuestAccessResponse(
    string State,
    string? ClientDisplayName,
    DateTimeOffset? ExpiresAtUtc,
    bool HasCreatedClient,
    int CreationProgressStep);

public sealed record PublicClientIntakeLocationResponse(Guid Id, string DisplayName);

public sealed record PublicClientIntakeLocationsResponse(
    string State,
    IReadOnlyList<PublicClientIntakeLocationResponse> Locations);

public sealed record PublicClientIntakeSupervisorResponse(
    Guid Id,
    string DisplayName,
    string JobTitle);

public sealed record PublicClientIntakeSupervisorsResponse(
    string State,
    IReadOnlyList<PublicClientIntakeSupervisorResponse> Supervisors);

public sealed record PublicClientIntakeIdentityRequest(
    string FirstName,
    string? MiddleName,
    string LastName,
    string? PreferredName,
    DateOnly DateOfBirth,
    string Gender,
    string PmiNumber,
    string? Ssn)
{
    public override string ToString() => "PublicClientIntakeIdentityRequest { Ssn = [REDACTED] }";
}

public sealed record PublicClientIntakeCareSetupRequest(
    DateOnly? IntakeDate,
    DateOnly SocDate,
    string? ReferralSource,
    Guid LocationId,
    Guid? SupervisorUserId,
    decimal? EvvAuthorizedHoursPerWeek);

public sealed record PublicClientIntakeServiceRequest(string Code, string Name);

public sealed record CreatePublicClientBasicDetailsRequest(
    PublicClientIntakeIdentityRequest Identity,
    PublicClientIntakeCareSetupRequest CareSetup,
    string? InsuranceProvider,
    IReadOnlyList<PublicClientIntakeServiceRequest>? Services)
{
    public override string ToString() => "CreatePublicClientBasicDetailsRequest { Identity.Ssn = [REDACTED] }";
}

public sealed record PublicClientIntakeContinuationResponse(
    string State,
    int NextStep,
    string EditorVersion,
    bool HasCreatedClient);

public sealed record PublicClientIntakeStateResponse(string State);

public sealed record PublicClientIntakeAdditionalInformationResponse(
    ClientAdditionalProfileRequest AdditionalInformation,
    string EditorVersion,
    int CreationProgressStep);

public sealed record UpdatePublicClientIntakeAdditionalInformationRequest(
    ClientAdditionalProfileRequest AdditionalInformation,
    string EditorVersion);

public sealed record PublicClientIntakeStepContinuationResponse(
    string EditorVersion,
    int NextStep);

public sealed record PublicClientIntakeContactInformation(
    string? Email, string? PrimaryPhone, string? SecondaryPhone, string? AddressLine1,
    string? AddressLine2, string? City, string? State, string? PostalCode, string? County,
    string? Country, IReadOnlyList<ClientLanguageRequest> PreferredLanguages);

public sealed record PublicClientIntakeContactInformationResponse(
    PublicClientIntakeContactInformation ContactInformation,
    string EditorVersion,
    int CreationProgressStep);

public sealed record UpdatePublicClientIntakeContactInformationRequest(
    PublicClientIntakeContactInformation ContactInformation,
    string EditorVersion);

public sealed record PublicClientCaregiverCandidateResponse(Guid Id, string DisplayName, string JobTitle);
public sealed record PublicClientCaregiverResponse(
    Guid AssignmentId, Guid CaregiverUserId, string CaregiverDisplayName, string JobTitle,
    string AssignmentType, string Relationship, bool? LivesWithClient, string AssignedServices,
    DateOnly StartDate, DateOnly? EndDate, string Status, bool ClientPreference, string Notes,
    string EditorVersion);
public sealed record PublicClientCaregiverListResponse(
    IReadOnlyList<PublicClientCaregiverResponse> Caregivers,
    IReadOnlyList<string> AvailableServices,
    int CreationProgressStep);
public sealed record SavePublicClientCaregiverRequest(
    Guid CaregiverUserId, string AssignmentType, bool ClientPreference, string? AssignedServices,
    DateOnly StartDate, DateOnly? EndDate, string? Relationship, bool? LivesWithClient,
    string? Notes, string? EditorVersion);
public sealed record EndPublicClientCaregiverRequest(DateOnly EndDate, string EditorVersion);
public sealed record SavePublicClientContactRequest(
    string? Type, string? Name, string? Relationship, string? Phone,
    string? Email, string? Address, bool? IsActive);
public sealed record PublicClientContactResponse(
    Guid Id, string Type, string Name, string Relationship, string Phone,
    string Email, string Address, bool IsActive);
public sealed record PublicClientContactListResponse(
    IReadOnlyList<PublicClientContactResponse> Contacts,
    int CreationProgressStep);
public sealed record SavePublicClientCareCoordinatorRequest(string? Name, string? CompanyName,
    string? Phone, string? PhoneExtension, string? Fax, string? Email, bool? IsActive);
public sealed record PublicClientCareCoordinatorResponse(Guid Id, string Name, string CompanyName,
    string Phone, string PhoneExtension, string Fax, string Email, bool IsActive);
public sealed record PublicClientCareCoordinatorListResponse(
    IReadOnlyList<PublicClientCareCoordinatorResponse> Coordinators, int CreationProgressStep);
public sealed record SavePublicClientConsultationProviderRequest(string? Name, string? Phone,
    string? Email, string? SupportProviderName, string? SupportProviderPhone,
    string? SupportProviderAddress, bool? IsActive);
public sealed record PublicClientConsultationProviderResponse(Guid Id, string Name, string Phone,
    string Email, string SupportProviderName, string SupportProviderPhone,
    string SupportProviderAddress, bool IsActive);
public sealed record PublicClientConsultationProviderListResponse(
    IReadOnlyList<PublicClientConsultationProviderResponse> Providers, int CreationProgressStep);
public sealed record SavePublicClientPhysicianRequest(string? Name, string? Npi, string? Phone,
    string? Fax, string? Address, string? ZipCode, string? City, string? State, bool? IsActive);
public sealed record PublicClientPhysicianResponse(Guid Id, string Name, string Npi, string Phone,
    string Fax, string Address, string ZipCode, string City, string State, bool IsActive);
public sealed record PublicClientPhysicianListResponse(
    IReadOnlyList<PublicClientPhysicianResponse> Physicians, int CreationProgressStep);
public sealed record SavePublicClientResponsiblePartyRequest(string? Name, string? Relationship,
    string? Phone, string? Email, string? Address, bool? LivesWithClient,
    bool? HasSpenddown, bool? IsActive);
public sealed record PublicClientResponsiblePartyResponse(Guid Id, string Name,
    string Relationship, string Phone, string Email, string Address,
    bool? LivesWithClient, bool? HasSpenddown, bool IsActive);
public sealed record PublicClientResponsiblePartyListResponse(
    IReadOnlyList<PublicClientResponsiblePartyResponse> ResponsibleParties,
    int CreationProgressStep);
public sealed record SavePublicClientHospitalizationRequest(
    string HospitalName, DateTimeOffset AdmittedAtUtc, DateTimeOffset? DischargedAtUtc,
    string Reason, string? Diagnosis, string? DischargeInstructions, string? EditorVersion);
public sealed record PublicClientHospitalizationResponse(
    Guid Id, string HospitalName, DateTimeOffset AdmittedAtUtc,
    DateTimeOffset? DischargedAtUtc, string Reason, string Diagnosis,
    string DischargeInstructions, string EditorVersion);
public sealed record PublicClientHospitalizationListResponse(
    IReadOnlyList<PublicClientHospitalizationResponse> Hospitalizations,
    int CreationProgressStep);
public sealed record PublicClientIntakeCompletionResponse(
    string State,
    DateTimeOffset CompletedAtUtc,
    string Message);
