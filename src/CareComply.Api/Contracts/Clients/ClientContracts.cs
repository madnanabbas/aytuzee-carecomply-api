namespace CareComply.Api.Contracts.Clients;

public sealed record ClientIdentityRequest(
    string FirstName,
    string? MiddleName,
    string LastName,
    string? PreferredName,
    DateOnly DateOfBirth,
    string Gender,
    string PmiNumber,
    string? Ssn = null)
{
    public override string ToString() => "ClientIdentityRequest { Ssn = [REDACTED] }";
}

public sealed record ClientSsnResponse(string Ssn);

public sealed record CreateClientGuestInvitationRequest(
    string ClientName,
    string Email,
    string? MobileNumber,
    Guid? CompanyId = null);

public sealed record CreateClientGuestInvitationResponse(
    Guid InvitationId,
    string GuestLink,
    DateTimeOffset ExpiresAtUtc);

public sealed record SendClientGuestInvitationRequest(string Token)
{
    public override string ToString() => "SendClientGuestInvitationRequest { Token = [REDACTED] }";
}

public sealed record SendClientGuestInvitationResponse(
    Guid InvitationId,
    DateTimeOffset SentAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record ClientContactRequest(
    string? InsuranceProvider,
    string? Email,
    string? PrimaryPhone,
    string? SecondaryPhone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? County,
    string? Country);

public sealed record ClientCareSetupRequest(
    DateOnly? IntakeDate,
    DateOnly SocDate,
    string? ReferralSource,
    string? Location,
    bool IsOnboarding,
    decimal? EvvAuthorizedHoursPerWeek,
    Guid? LocationId = null,
    Guid? SupervisorUserId = null);

public sealed record ClientSupervisorCandidateResponse(Guid Id, string Name, string JobTitle);

public sealed record ClientSafetyRequest(
    string? SafetyMeasures,
    string? SpecialEquipment,
    string? Allergies,
    string? OtherServices);

public sealed record ClientAdditionalProfileRequest(
    string ProgramType,
    string? ReferralDetails,
    string? HomeCareRating,
    string? MedicalEquipment,
    string? PcaComplexity,
    string? Demographics,
    bool? Smokes,
    bool? IsVeteran,
    bool? HasPets,
    string? LivingArrangement,
    string? PreferredClinic,
    string? PreferredHospital,
    string? PreferredAmbulance,
    string? CaregiverPreference,
    string? NutritionalRequirements,
    string? Diet,
    string? MealSchedule,
    string? Notes,
    string? AdditionalInformation);

public sealed record ClientServiceRequest(string Code, string Name);

public sealed record ClientLanguageRequest(
    string LanguageCode,
    string LanguageName,
    bool IsPrimary);

public sealed record CreateClientRequest(
    ClientIdentityRequest Identity,
    ClientContactRequest Contact,
    ClientCareSetupRequest CareSetup,
    ClientSafetyRequest Safety,
    ClientAdditionalProfileRequest AdditionalProfile,
    string InitialStatus,
    DateOnly? DischargeDate,
    string? DischargeReason,
    IReadOnlyList<ClientServiceRequest>? Services,
    IReadOnlyList<ClientLanguageRequest>? Languages);

public sealed record CreateClientBasicDetailsRequest(
    ClientIdentityRequest Identity,
    ClientCareSetupRequest CareSetup,
    string? InsuranceProvider,
    IReadOnlyList<ClientServiceRequest>? Services);

public sealed record UpdateClientBasicDetailsRequest(
    ClientIdentityRequest Identity,
    ClientCareSetupRequest CareSetup,
    string? InsuranceProvider,
    IReadOnlyList<ClientServiceRequest>? Services,
    string EditorVersion);

public sealed record UpdateClientAdditionalInformationRequest(
    ClientAdditionalProfileRequest AdditionalProfile,
    string EditorVersion);

public sealed record UpdateClientContactInformationRequest(
    string? Email,
    string? PrimaryPhone,
    string? SecondaryPhone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? County,
    string? Country,
    IReadOnlyList<ClientLanguageRequest>? PreferredLanguages,
    string EditorVersion);

public sealed record ClientMutationResponse(Guid Id, string EditorVersion);

public sealed record UpdateClientRequest(
    ClientIdentityRequest Identity,
    ClientContactRequest Contact,
    ClientCareSetupRequest CareSetup,
    ClientSafetyRequest Safety,
    ClientAdditionalProfileRequest AdditionalProfile,
    IReadOnlyList<ClientServiceRequest>? Services,
    IReadOnlyList<ClientLanguageRequest>? Languages,
    DateOnly? ServiceChangeEffectiveDate,
    string EditorVersion);

public sealed record ChangeClientStatusRequest(
    string Status,
    DateOnly? DischargeDate,
    string? DischargeReason,
    string EditorVersion);

public sealed record CompleteClientCreationRequest(
    string EditorVersion,
    int? Status = null,
    DateOnly? DischargeDate = null,
    string? DischargeReason = null);

public sealed record CompleteClientCreationResponse(
    Guid Id,
    string Status,
    string EditorVersion);

public sealed record AdvanceClientCreationProgressRequest(
    int Step,
    string EditorVersion);

public sealed record ClientCreationProgressResponse(
    Guid Id,
    int CurrentStep,
    string EditorVersion);

public sealed record ClientCreationStateResponse(
    Guid Id,
    string Status,
    int CurrentStep,
    string EditorVersion,
    ClientIdentityResponse Identity,
    ClientContactResponse Contact,
    ClientCareSetupResponse CareSetup,
    ClientAdditionalProfileResponse AdditionalProfile,
    IReadOnlyList<ClientServiceResponse> Services,
    IReadOnlyList<ClientLanguageResponse> Languages);

public sealed record ClientListResponse(
    IReadOnlyList<ClientListItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record ClientListItemResponse(
    Guid Id,
    string DisplayName,
    string PreferredName,
    string PmiNumber,
    DateOnly DateOfBirth,
    string Gender,
    string Status,
    string Location,
    DateOnly SocDate,
    bool IsOnboarding,
    string ProgramType,
    IReadOnlyList<ClientServiceResponse> Services,
    DateTimeOffset UpdatedAtUtc);

public sealed record ClientDetailsResponse(
    Guid Id,
    ClientIdentityResponse Identity,
    ClientContactResponse Contact,
    ClientCareSetupResponse CareSetup,
    ClientSafetyResponse Safety,
    ClientAdditionalProfileResponse AdditionalProfile,
    IReadOnlyList<ClientServiceResponse> Services,
    IReadOnlyList<ClientLanguageResponse> Languages,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string EditorVersion);

public sealed record ClientIdentityResponse(
    string FirstName,
    string MiddleName,
    string LastName,
    string PreferredName,
    DateOnly DateOfBirth,
    string Gender,
    string PmiNumber,
    bool HasSsn);

public sealed record ClientContactResponse(
    string InsuranceProvider,
    string Email,
    string PrimaryPhone,
    string SecondaryPhone,
    string AddressLine1,
    string AddressLine2,
    string City,
    string State,
    string PostalCode,
    string County,
    string Country);

public sealed record ClientCareSetupResponse(
    DateOnly? IntakeDate,
    DateOnly SocDate,
    string ReferralSource,
    string Status,
    string Location,
    bool IsOnboarding,
    decimal? EvvAuthorizedHoursPerWeek,
    DateOnly? DischargeDate,
    string DischargeReason,
    Guid LocationId,
    Guid? SupervisorUserId,
    string SupervisorName);

public sealed record ClientSafetyResponse(
    string SafetyMeasures,
    string SpecialEquipment,
    string Allergies,
    string OtherServices);

public sealed record ClientAdditionalProfileResponse(
    string ProgramType,
    string ReferralDetails,
    string HomeCareRating,
    string MedicalEquipment,
    string PcaComplexity,
    string Demographics,
    bool? Smokes,
    bool? IsVeteran,
    bool? HasPets,
    string LivingArrangement,
    string PreferredClinic,
    string PreferredHospital,
    string PreferredAmbulance,
    string CaregiverPreference,
    string NutritionalRequirements,
    string Diet,
    string MealSchedule,
    string Notes,
    string AdditionalInformation);

public sealed record ClientServiceResponse(
    Guid Id,
    string Code,
    string Name,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive);

public sealed record ClientLanguageResponse(
    string LanguageCode,
    string LanguageName,
    bool IsPrimary);
