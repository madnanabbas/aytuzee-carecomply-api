using CareComply.Application.Abstractions.Clients;
using CareComply.Domain.Clients;

namespace CareComply.Infrastructure.Clients;

internal static class ClientProfilePersistence
{
    public static void Apply(
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

    public static bool HasChanges(
        ClientAdditionalProfile? profile,
        ClientAdditionalProfileInput input)
    {
        if (profile is null)
        {
            return true;
        }

        return profile.ProgramType != input.ProgramType
            || !ClientChangeDetector.Equal(profile.ReferralDetails, input.ReferralDetails)
            || !ClientChangeDetector.Equal(profile.HomeCareRating, input.HomeCareRating)
            || !ClientChangeDetector.Equal(profile.MedicalEquipment, input.MedicalEquipment)
            || !ClientChangeDetector.Equal(profile.PcaComplexity, input.PcaComplexity)
            || !ClientChangeDetector.Equal(profile.Demographics, input.Demographics)
            || profile.Smokes != input.Smokes
            || profile.IsVeteran != input.IsVeteran
            || profile.HasPets != input.HasPets
            || !ClientChangeDetector.Equal(profile.LivingArrangement, input.LivingArrangement)
            || !ClientChangeDetector.Equal(profile.PreferredClinic, input.PreferredClinic)
            || !ClientChangeDetector.Equal(profile.PreferredHospital, input.PreferredHospital)
            || !ClientChangeDetector.Equal(profile.PreferredAmbulance, input.PreferredAmbulance)
            || !ClientChangeDetector.Equal(profile.CaregiverPreference, input.CaregiverPreference)
            || !ClientChangeDetector.Equal(profile.NutritionalRequirements, input.NutritionalRequirements)
            || !ClientChangeDetector.Equal(profile.Diet, input.Diet)
            || !ClientChangeDetector.Equal(profile.MealSchedule, input.MealSchedule)
            || !ClientChangeDetector.Equal(profile.Notes, input.Notes)
            || !ClientChangeDetector.Equal(profile.AdditionalInformation, input.AdditionalInformation);
    }
}
