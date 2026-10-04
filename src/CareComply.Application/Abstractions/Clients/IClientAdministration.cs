using CareComply.Domain.Clients;

namespace CareComply.Application.Abstractions.Clients;

public interface IClientAdministration
{
    Task<ClientAdministrationResult> CreateBasicDetailsAsync(
        ClientBasicDetailsCreateRequest request,
        CancellationToken cancellationToken);

    Task<ClientAdministrationResult> UpdateBasicDetailsAsync(
        ClientBasicDetailsUpdateRequest request,
        CancellationToken cancellationToken);

    Task<ClientAdministrationResult> UpdateAdditionalInformationAsync(
        ClientAdditionalInformationUpdateRequest request,
        CancellationToken cancellationToken);

    Task<ClientAdministrationResult> UpdateContactInformationAsync(
        ClientContactInformationUpdateRequest request,
        CancellationToken cancellationToken);

    Task<ClientAdministrationResult> CreateAsync(
        ClientCreateRequest request,
        CancellationToken cancellationToken);

    Task<ClientAdministrationResult> UpdateAsync(
        ClientUpdateRequest request,
        CancellationToken cancellationToken);

    Task<ClientAdministrationResult> ChangeStatusAsync(
        ClientStatusChangeRequest request,
        CancellationToken cancellationToken);

    Task<ClientAdministrationResult> CompleteCreationAsync(
        ClientCreationCompletionRequest request,
        CancellationToken cancellationToken);

    Task<ClientCreationStateResult> GetCreationStateAsync(
        Guid clientId,
        CancellationToken cancellationToken);

    Task<ClientCreationProgressResult> AdvanceCreationProgressAsync(
        ClientCreationProgressRequest request,
        CancellationToken cancellationToken);
}

public sealed record ClientBasicDetailsCreateRequest(
    ClientIdentityInput Identity,
    ClientCareSetupInput CareSetup,
    string? InsuranceProvider,
    IReadOnlyList<ClientServiceInput> Services);

public sealed record ClientBasicDetailsUpdateRequest(
    Guid ClientId,
    ClientIdentityInput Identity,
    ClientCareSetupInput CareSetup,
    string? InsuranceProvider,
    IReadOnlyList<ClientServiceInput> Services,
    string EditorVersion);

public sealed record ClientAdditionalInformationUpdateRequest(
    Guid ClientId,
    ClientAdditionalProfileInput AdditionalProfile,
    string EditorVersion);

public sealed record ClientContactInformationUpdateRequest(
    Guid ClientId,
    ClientContactInput Contact,
    IReadOnlyList<ClientLanguageInput> Languages,
    string EditorVersion);

public sealed record ClientIdentityInput(
    string FirstName,
    string? MiddleName,
    string LastName,
    string? PreferredName,
    DateOnly DateOfBirth,
    ClientGender Gender,
    string PmiNumber,
    string? Ssn = null)
{
    public override string ToString() => "ClientIdentityInput { Ssn = [REDACTED] }";
}

public interface IClientSensitiveData
{
    string Protect(string ssn);
    string Unprotect(string protectedSsn);
    Task<string?> RevealSsnAsync(Guid clientId, CancellationToken cancellationToken);
}

public sealed record ClientContactInput(
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

public sealed record ClientCareSetupInput(
    DateOnly? IntakeDate,
    DateOnly SocDate,
    string? ReferralSource,
    string? Location,
    bool IsOnboarding,
    decimal? EvvAuthorizedHoursPerWeek,
    Guid? LocationId = null,
    Guid? SupervisorUserId = null);

public sealed record ClientSafetyInput(
    string? SafetyMeasures,
    string? SpecialEquipment,
    string? Allergies,
    string? OtherServices);

public sealed record ClientAdditionalProfileInput(
    ClientProgramType ProgramType,
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

public sealed record ClientServiceInput(string Code, string Name);

public sealed record ClientLanguageInput(
    string LanguageCode,
    string LanguageName,
    bool IsPrimary);

public sealed record ClientCreateRequest(
    ClientIdentityInput Identity,
    ClientContactInput Contact,
    ClientCareSetupInput CareSetup,
    ClientSafetyInput Safety,
    ClientAdditionalProfileInput AdditionalProfile,
    ClientStatus InitialStatus,
    DateOnly? DischargeDate,
    string? DischargeReason,
    IReadOnlyList<ClientServiceInput> Services,
    IReadOnlyList<ClientLanguageInput> Languages);

public sealed record ClientUpdateRequest(
    Guid ClientId,
    ClientIdentityInput Identity,
    ClientContactInput Contact,
    ClientCareSetupInput CareSetup,
    ClientSafetyInput Safety,
    ClientAdditionalProfileInput AdditionalProfile,
    IReadOnlyList<ClientServiceInput> Services,
    IReadOnlyList<ClientLanguageInput> Languages,
    DateOnly? ServiceChangeEffectiveDate,
    string EditorVersion);

public sealed record ClientStatusChangeRequest(
    Guid ClientId,
    ClientStatus Status,
    DateOnly? DischargeDate,
    string? DischargeReason,
    string EditorVersion);

public sealed record ClientCreationCompletionRequest(
    Guid ClientId,
    string EditorVersion);

public sealed record ClientCreationProgressRequest(
    Guid ClientId,
    int Step,
    string EditorVersion);

public sealed record ClientCreationState(
    ClientDirectoryDetails Client,
    int CurrentStep);

public sealed record ClientCreationStateResult(
    ClientAdministrationStatus Status,
    ClientCreationState? State = null);

public sealed record ClientCreationProgressResult(
    ClientAdministrationStatus Status,
    Guid ClientId,
    int CurrentStep,
    string? EditorVersion = null);

public sealed record ClientAdministrationResult(
    ClientAdministrationStatus Status,
    ClientDirectoryDetails? Client,
    IReadOnlyList<string> ChangedSections,
    ClientStatus? PreviousStatus = null);

public enum ClientAdministrationStatus
{
    Succeeded,
    NoChanges,
    NotFound,
    Conflict,
    DuplicatePmi,
    InvalidState,
    InvalidServiceChange,
    ServiceHistoryConflict,
}
