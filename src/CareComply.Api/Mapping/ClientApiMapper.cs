using CareComply.Api.Contracts.Clients;
using CareComply.Application.Abstractions.Clients;
using CareComply.Domain.Clients;

namespace CareComply.Api.Mapping;

internal static class ClientApiMapper
{
    public static ClientIdentityInput ToInput(ClientIdentityRequest request, ClientGender gender) => new(
        request.FirstName,
        request.MiddleName,
        request.LastName,
        request.PreferredName,
        request.DateOfBirth,
        gender,
        request.PmiNumber,
        request.Ssn);

    public static ClientContactInput ToInput(ClientContactRequest request) => new(
        request.InsuranceProvider,
        request.Email,
        request.PrimaryPhone,
        request.SecondaryPhone,
        request.AddressLine1,
        request.AddressLine2,
        request.City,
        request.State,
        request.PostalCode,
        request.County,
        request.Country);

    public static ClientCareSetupInput ToInput(ClientCareSetupRequest request) => new(
        request.IntakeDate,
        request.SocDate,
        request.ReferralSource,
        request.Location,
        request.IsOnboarding,
        request.EvvAuthorizedHoursPerWeek,
        request.LocationId,
        request.SupervisorUserId);

    public static ClientSafetyInput ToInput(ClientSafetyRequest request) => new(
        request.SafetyMeasures,
        request.SpecialEquipment,
        request.Allergies,
        request.OtherServices);

    public static ClientAdditionalProfileInput ToInput(
        ClientAdditionalProfileRequest request,
        ClientProgramType programType) => new(
        programType,
        request.ReferralDetails,
        request.HomeCareRating,
        request.MedicalEquipment,
        request.PcaComplexity,
        request.Demographics,
        request.Smokes,
        request.IsVeteran,
        request.HasPets,
        request.LivingArrangement,
        request.PreferredClinic,
        request.PreferredHospital,
        request.PreferredAmbulance,
        request.CaregiverPreference,
        request.NutritionalRequirements,
        request.Diet,
        request.MealSchedule,
        request.Notes,
        request.AdditionalInformation);

    public static ClientServiceInput[] ToInputs(IReadOnlyList<ClientServiceRequest>? services) =>
        services?.Select(service => new ClientServiceInput(service.Code, service.Name)).ToArray() ?? [];

    public static ClientLanguageInput[] ToInputs(IReadOnlyList<ClientLanguageRequest>? languages) =>
        languages?.Select(language => new ClientLanguageInput(
            language.LanguageCode,
            language.LanguageName,
            language.IsPrimary)).ToArray() ?? [];

    public static ClientSupervisorCandidateResponse ToResponse(ClientSupervisorCandidate candidate) => new(
        candidate.Id,
        candidate.DisplayName,
        candidate.JobTitle);

    public static ClientListItemResponse ToResponse(ClientDirectoryItem client) => new(
        client.Id,
        BuildDisplayName(client.FirstName, client.MiddleName, client.LastName),
        client.PreferredName,
        client.PmiNumber,
        client.DateOfBirth,
        client.Gender.ToString(),
        client.Status.ToString(),
        client.Location,
        client.SocDate,
        client.IsOnboarding,
        client.ProgramType.ToString(),
        client.Services.Select(ToResponse).ToArray(),
        client.UpdatedAtUtc);

    public static ClientDetailsResponse ToResponse(ClientDirectoryDetails client) => new(
        client.Id,
        new(
            client.FirstName,
            client.MiddleName,
            client.LastName,
            client.PreferredName,
            client.DateOfBirth,
            client.Gender.ToString(),
            client.PmiNumber,
            client.HasSsn),
        new(
            client.InsuranceProvider,
            client.Email,
            client.PrimaryPhone,
            client.SecondaryPhone,
            client.AddressLine1,
            client.AddressLine2,
            client.City,
            client.State,
            client.PostalCode,
            client.County,
            client.Country),
        new(
            client.IntakeDate,
            client.SocDate,
            client.ReferralSource,
            client.Status.ToString(),
            client.Location,
            client.IsOnboarding,
            client.EvvAuthorizedHoursPerWeek,
            client.DischargeDate,
            client.DischargeReason,
            client.LocationId,
            client.SupervisorUserId),
        new(
            client.SafetyMeasures,
            client.SpecialEquipment,
            client.Allergies,
            client.OtherServices),
        new(
            client.AdditionalProfile.ProgramType.ToString(),
            client.AdditionalProfile.ReferralDetails,
            client.AdditionalProfile.HomeCareRating,
            client.AdditionalProfile.MedicalEquipment,
            client.AdditionalProfile.PcaComplexity,
            client.AdditionalProfile.Demographics,
            client.AdditionalProfile.Smokes,
            client.AdditionalProfile.IsVeteran,
            client.AdditionalProfile.HasPets,
            client.AdditionalProfile.LivingArrangement,
            client.AdditionalProfile.PreferredClinic,
            client.AdditionalProfile.PreferredHospital,
            client.AdditionalProfile.PreferredAmbulance,
            client.AdditionalProfile.CaregiverPreference,
            client.AdditionalProfile.NutritionalRequirements,
            client.AdditionalProfile.Diet,
            client.AdditionalProfile.MealSchedule,
            client.AdditionalProfile.Notes,
            client.AdditionalProfile.AdditionalInformation),
        client.Services.Select(ToResponse).ToArray(),
        client.Languages.Select(language => new ClientLanguageResponse(
            language.LanguageCode,
            language.LanguageName,
            language.IsPrimary)).ToArray(),
        client.CreatedAtUtc,
        client.UpdatedAtUtc,
        client.EditorVersion);

    private static ClientServiceResponse ToResponse(ClientDirectoryService service) => new(
        service.Id,
        service.Code,
        service.Name,
        service.StartDate,
        service.EndDate,
        service.IsActive);

    private static string BuildDisplayName(string firstName, string middleName, string lastName) =>
        string.IsNullOrWhiteSpace(middleName)
            ? $"{firstName} {lastName}"
            : $"{firstName} {middleName} {lastName}";
}
