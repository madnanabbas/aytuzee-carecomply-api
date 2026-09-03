namespace CareComply.Domain.Clients;

public sealed class ClientAdditionalProfile
{
    public const int MaximumShortTextLength = 200;
    public const int MaximumLongTextLength = 2000;

    private ClientAdditionalProfile()
    {
    }

    public ClientAdditionalProfile(Guid clientId)
    {
        if (clientId == Guid.Empty)
        {
            throw new ArgumentException("A client identifier is required.", nameof(clientId));
        }

        ClientId = clientId;
    }

    public Guid ClientId { get; private set; }
    public ClientProgramType ProgramType { get; private set; }
    public string ReferralDetails { get; private set; } = string.Empty;
    public string HomeCareRating { get; private set; } = string.Empty;
    public string MedicalEquipment { get; private set; } = string.Empty;
    public string PcaComplexity { get; private set; } = string.Empty;
    public string Demographics { get; private set; } = string.Empty;
    public bool? Smokes { get; private set; }
    public bool? IsVeteran { get; private set; }
    public bool? HasPets { get; private set; }
    public string LivingArrangement { get; private set; } = string.Empty;
    public string PreferredClinic { get; private set; } = string.Empty;
    public string PreferredHospital { get; private set; } = string.Empty;
    public string PreferredAmbulance { get; private set; } = string.Empty;
    public string CaregiverPreference { get; private set; } = string.Empty;
    public string NutritionalRequirements { get; private set; } = string.Empty;
    public string Diet { get; private set; } = string.Empty;
    public string MealSchedule { get; private set; } = string.Empty;
    public string Notes { get; private set; } = string.Empty;
    public string AdditionalInformation { get; private set; } = string.Empty;

    public void Change(
        ClientProgramType programType,
        string? referralDetails,
        string? homeCareRating,
        string? medicalEquipment,
        string? pcaComplexity,
        string? demographics,
        bool? smokes,
        bool? isVeteran,
        bool? hasPets,
        string? livingArrangement,
        string? preferredClinic,
        string? preferredHospital,
        string? preferredAmbulance,
        string? caregiverPreference,
        string? nutritionalRequirements,
        string? diet,
        string? mealSchedule,
        string? notes,
        string? additionalInformation)
    {
        if (!Enum.IsDefined(programType))
        {
            throw new ArgumentOutOfRangeException(nameof(programType));
        }

        ProgramType = programType;
        ReferralDetails = NormalizeLong(referralDetails, nameof(referralDetails));
        HomeCareRating = NormalizeShort(homeCareRating, nameof(homeCareRating));
        MedicalEquipment = NormalizeLong(medicalEquipment, nameof(medicalEquipment));
        PcaComplexity = NormalizeShort(pcaComplexity, nameof(pcaComplexity));
        Demographics = NormalizeLong(demographics, nameof(demographics));
        Smokes = smokes;
        IsVeteran = isVeteran;
        HasPets = hasPets;
        LivingArrangement = NormalizeLong(livingArrangement, nameof(livingArrangement));
        PreferredClinic = NormalizeShort(preferredClinic, nameof(preferredClinic));
        PreferredHospital = NormalizeShort(preferredHospital, nameof(preferredHospital));
        PreferredAmbulance = NormalizeShort(preferredAmbulance, nameof(preferredAmbulance));
        CaregiverPreference = NormalizeLong(caregiverPreference, nameof(caregiverPreference));
        NutritionalRequirements = NormalizeLong(nutritionalRequirements, nameof(nutritionalRequirements));
        Diet = NormalizeLong(diet, nameof(diet));
        MealSchedule = NormalizeLong(mealSchedule, nameof(mealSchedule));
        Notes = NormalizeLong(notes, nameof(notes));
        AdditionalInformation = NormalizeLong(additionalInformation, nameof(additionalInformation));
    }

    private static string NormalizeShort(string? value, string parameterName) =>
        Normalize(value, MaximumShortTextLength, parameterName);

    private static string NormalizeLong(string? value, string parameterName) =>
        Normalize(value, MaximumLongTextLength, parameterName);

    private static string Normalize(string? value, int maxLength, string parameterName)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Value cannot exceed {maxLength} characters.");
        }

        return normalized;
    }
}
