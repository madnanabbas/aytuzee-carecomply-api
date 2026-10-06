using CareComply.Domain.Clients;

namespace CareComply.Application.Abstractions.Clients;

public interface IClientDirectory
{
    Task<ClientDirectoryPage> ListAsync(
        ClientDirectoryRequest request,
        CancellationToken cancellationToken);

    Task<ClientDirectoryDetails?> GetByIdAsync(
        Guid clientId,
        CancellationToken cancellationToken);
}

public sealed record ClientDirectoryRequest(
    string? Search,
    ClientStatus? Status,
    string? Location,
    ClientProgramType? ProgramType,
    string? ServiceCode,
    bool? IsOnboarding,
    ClientDirectorySort Sort,
    bool SortDescending,
    int PageNumber,
    int PageSize);

public enum ClientDirectorySort
{
    Name,
    SocDate,
    Status,
    Location,
    Updated,
}

public sealed record ClientDirectoryPage(
    IReadOnlyList<ClientDirectoryItem> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);

public sealed record ClientDirectoryItem(
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
    IReadOnlyList<ClientDirectoryService> Services,
    DateTimeOffset UpdatedAtUtc);

public sealed record ClientDirectoryDetails(
    Guid Id,
    string FirstName,
    string MiddleName,
    string LastName,
    string PreferredName,
    DateOnly DateOfBirth,
    ClientGender Gender,
    string PmiNumber,
    bool HasSsn,
    string InsuranceProvider,
    string Email,
    string PrimaryPhone,
    string SecondaryPhone,
    DateOnly? IntakeDate,
    DateOnly SocDate,
    string ReferralSource,
    ClientStatus Status,
    string Location,
    bool IsOnboarding,
    DateOnly? DischargeDate,
    string DischargeReason,
    string AddressLine1,
    string AddressLine2,
    string City,
    string State,
    string PostalCode,
    string County,
    string Country,
    decimal? EvvAuthorizedHoursPerWeek,
    string SafetyMeasures,
    string SpecialEquipment,
    string Allergies,
    string OtherServices,
    ClientDirectoryAdditionalProfile AdditionalProfile,
    IReadOnlyList<ClientDirectoryService> Services,
    IReadOnlyList<ClientDirectoryLanguage> Languages,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string EditorVersion,
    Guid LocationId,
    Guid? SupervisorUserId,
    string SupervisorName);

public sealed record ClientDirectoryAdditionalProfile(
    ClientProgramType ProgramType,
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

public sealed record ClientDirectoryService(
    Guid Id,
    string Code,
    string Name,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive);

public sealed record ClientDirectoryLanguage(
    string LanguageCode,
    string LanguageName,
    bool IsPrimary);
