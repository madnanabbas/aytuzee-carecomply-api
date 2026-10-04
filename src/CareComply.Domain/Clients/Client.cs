namespace CareComply.Domain.Clients;

public sealed class Client
{
    public const int MaximumNameLength = 100;
    public const int MaximumPreferredNameLength = 100;
    public const int MaximumPmiLength = 50;
    public const int MaximumInsuranceLength = 200;
    public const int MaximumEmailLength = 320;
    public const int MaximumPhoneLength = 30;
    public const int MaximumLocationLength = 150;
    public const int MaximumReferralSourceLength = 200;
    public const int MaximumAddressLineLength = 200;
    public const int MaximumCityLength = 100;
    public const int MaximumStateLength = 100;
    public const int MaximumPostalCodeLength = 20;
    public const int MaximumCountyLength = 100;
    public const int MaximumCountryLength = 100;
    public const int MaximumTextLength = 2000;
    public const int MaximumProtectedSsnLength = 1000;
    public const decimal MaximumEvvAuthorizedHoursPerWeek = 168m;

    private Client()
    {
    }

    private Client(
        Guid id,
        Guid companyId,
        string firstName,
        string? middleName,
        string lastName,
        string? preferredName,
        DateOnly dateOfBirth,
        ClientGender gender,
        string pmiNumber,
        DateOnly socDate,
        DateOnly? intakeDate,
        ClientStatus status,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A client identifier is required.", nameof(id));
        }

        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("A company identifier is required.", nameof(companyId));
        }

        Id = id;
        CompanyId = companyId;
        SetIdentity(firstName, middleName, lastName, preferredName, dateOfBirth, gender, pmiNumber);
        SetIntakeAndStatus(intakeDate, socDate, status, null, null);
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid LocationId { get; private set; }
    public Guid? SupervisorUserId { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string MiddleName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string PreferredName { get; private set; } = string.Empty;
    public DateOnly DateOfBirth { get; private set; }
    public ClientGender Gender { get; private set; }
    public string PmiNumber { get; private set; } = string.Empty;
    public string ProtectedSsn { get; private set; } = string.Empty;
    public string InsuranceProvider { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PrimaryPhone { get; private set; } = string.Empty;
    public string SecondaryPhone { get; private set; } = string.Empty;
    public DateOnly? IntakeDate { get; private set; }
    public DateOnly SocDate { get; private set; }
    public string ReferralSource { get; private set; } = string.Empty;
    public ClientStatus Status { get; private set; }
    public int CreationProgressStep { get; private set; }
    public string Location { get; private set; } = string.Empty;
    public bool IsOnboarding { get; private set; }
    public DateOnly? DischargeDate { get; private set; }
    public string DischargeReason { get; private set; } = string.Empty;
    public string AddressLine1 { get; private set; } = string.Empty;
    public string AddressLine2 { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public string PostalCode { get; private set; } = string.Empty;
    public string County { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;
    public decimal? EvvAuthorizedHoursPerWeek { get; private set; }
    public string SafetyMeasures { get; private set; } = string.Empty;
    public string SpecialEquipment { get; private set; } = string.Empty;
    public string Allergies { get; private set; } = string.Empty;
    public string OtherServices { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void AssignSupervisor(Guid? supervisorUserId, DateTimeOffset nowUtc)
    {
        if (supervisorUserId == Guid.Empty) throw new ArgumentException("A valid supervisor identifier is required.", nameof(supervisorUserId));
        if (SupervisorUserId == supervisorUserId) return;
        SupervisorUserId = supervisorUserId;
        UpdatedAtUtc = nowUtc;
    }

    public static Client Create(
        Guid id,
        Guid companyId,
        string firstName,
        string? middleName,
        string lastName,
        string? preferredName,
        DateOnly dateOfBirth,
        ClientGender gender,
        string pmiNumber,
        DateOnly socDate,
        DateOnly? intakeDate,
        ClientStatus status,
        DateTimeOffset createdAtUtc) =>
        new(id, companyId, firstName, middleName, lastName, preferredName, dateOfBirth, gender, pmiNumber,
            socDate, intakeDate, status, createdAtUtc);

    public void ChangeIdentity(
        string firstName,
        string? middleName,
        string lastName,
        string? preferredName,
        DateOnly dateOfBirth,
        ClientGender gender,
        string pmiNumber,
        DateTimeOffset updatedAtUtc)
    {
        SetIdentity(firstName, middleName, lastName, preferredName, dateOfBirth, gender, pmiNumber);
        Touch(updatedAtUtc);
    }

    public void ChangeProtectedSsn(string? protectedSsn, DateTimeOffset updatedAtUtc)
    {
        ProtectedSsn = Normalize(protectedSsn, MaximumProtectedSsnLength, nameof(protectedSsn));
        Touch(updatedAtUtc);
    }

    public void ChangeContact(
        string? email,
        string? primaryPhone,
        string? secondaryPhone,
        string? addressLine1,
        string? addressLine2,
        string? city,
        string? state,
        string? postalCode,
        string? county,
        string? country,
        DateTimeOffset updatedAtUtc)
    {
        Email = Normalize(email, MaximumEmailLength, nameof(email));
        PrimaryPhone = Normalize(primaryPhone, MaximumPhoneLength, nameof(primaryPhone));
        SecondaryPhone = Normalize(secondaryPhone, MaximumPhoneLength, nameof(secondaryPhone));
        AddressLine1 = Normalize(addressLine1, MaximumAddressLineLength, nameof(addressLine1));
        AddressLine2 = Normalize(addressLine2, MaximumAddressLineLength, nameof(addressLine2));
        City = Normalize(city, MaximumCityLength, nameof(city));
        State = Normalize(state, MaximumStateLength, nameof(state));
        PostalCode = Normalize(postalCode, MaximumPostalCodeLength, nameof(postalCode));
        County = Normalize(county, MaximumCountyLength, nameof(county));
        Country = Normalize(country, MaximumCountryLength, nameof(country));
        Touch(updatedAtUtc);
    }

    public void ChangeCareSetup(
        DateOnly? intakeDate,
        DateOnly socDate,
        ClientStatus status,
        string? referralSource,
        string? location,
        string? insuranceProvider,
        bool isOnboarding,
        decimal? evvAuthorizedHoursPerWeek,
        DateOnly? dischargeDate,
        string? dischargeReason,
        DateTimeOffset updatedAtUtc)
    {
        SetIntakeAndStatus(intakeDate, socDate, status, dischargeDate, dischargeReason);
        ReferralSource = Normalize(referralSource, MaximumReferralSourceLength, nameof(referralSource));
        Location = Normalize(location, MaximumLocationLength, nameof(location));
        InsuranceProvider = Normalize(insuranceProvider, MaximumInsuranceLength, nameof(insuranceProvider));
        IsOnboarding = isOnboarding;

        if (evvAuthorizedHoursPerWeek is < 0 or > MaximumEvvAuthorizedHoursPerWeek)
        {
            throw new ArgumentOutOfRangeException(nameof(evvAuthorizedHoursPerWeek),
                $"EVV authorized hours must be between 0 and {MaximumEvvAuthorizedHoursPerWeek} hours per week.");
        }

        EvvAuthorizedHoursPerWeek = evvAuthorizedHoursPerWeek;
        Touch(updatedAtUtc);
    }

    public void AssignLocation(Guid locationId, string locationName, DateTimeOffset updatedAtUtc)
    {
        if (locationId == Guid.Empty) throw new ArgumentException("A location identifier is required.", nameof(locationId));
        LocationId = locationId;
        Location = Normalize(locationName, MaximumLocationLength, nameof(locationName));
        Touch(updatedAtUtc);
    }

    public void ChangeSafety(
        string? safetyMeasures,
        string? specialEquipment,
        string? allergies,
        string? otherServices,
        DateTimeOffset updatedAtUtc)
    {
        SafetyMeasures = Normalize(safetyMeasures, MaximumTextLength, nameof(safetyMeasures));
        SpecialEquipment = Normalize(specialEquipment, MaximumTextLength, nameof(specialEquipment));
        Allergies = Normalize(allergies, MaximumTextLength, nameof(allergies));
        OtherServices = Normalize(otherServices, MaximumTextLength, nameof(otherServices));
        Touch(updatedAtUtc);
    }

    public void ChangeStatus(
        ClientStatus status,
        DateOnly? dischargeDate,
        string? dischargeReason,
        DateTimeOffset updatedAtUtc)
    {
        SetIntakeAndStatus(IntakeDate, SocDate, status, dischargeDate, dischargeReason);
        Touch(updatedAtUtc);
    }

    public bool AdvanceCreationProgress(int step, DateTimeOffset nowUtc)
    {
        if (Status != ClientStatus.Pending)
            throw new InvalidOperationException("Only pending clients have creation progress.");
        if (step is < 2 or > 10)
            throw new ArgumentOutOfRangeException(nameof(step));
        if (step <= CreationProgressStep) return false;
        if (CreationProgressStep > 0 && step > CreationProgressStep + 1)
            throw new InvalidOperationException("Creation progress cannot skip steps.");

        CreationProgressStep = step;
        Touch(nowUtc);
        return true;
    }

    public void MarkUpdated(DateTimeOffset updatedAtUtc) => Touch(updatedAtUtc);

    private void SetIdentity(
        string firstName,
        string? middleName,
        string lastName,
        string? preferredName,
        DateOnly dateOfBirth,
        ClientGender gender,
        string pmiNumber)
    {
        if (dateOfBirth == default)
        {
            throw new ArgumentOutOfRangeException(nameof(dateOfBirth), "Date of birth is required.");
        }

        if (!Enum.IsDefined(gender))
        {
            throw new ArgumentOutOfRangeException(nameof(gender));
        }

        FirstName = NormalizeRequired(firstName, MaximumNameLength, nameof(firstName));
        MiddleName = Normalize(middleName, MaximumNameLength, nameof(middleName));
        LastName = NormalizeRequired(lastName, MaximumNameLength, nameof(lastName));
        PreferredName = Normalize(preferredName, MaximumPreferredNameLength, nameof(preferredName));
        DateOfBirth = dateOfBirth;
        Gender = gender;
        PmiNumber = NormalizeRequired(pmiNumber, MaximumPmiLength, nameof(pmiNumber));
    }

    private void SetIntakeAndStatus(
        DateOnly? intakeDate,
        DateOnly socDate,
        ClientStatus status,
        DateOnly? dischargeDate,
        string? dischargeReason)
    {
        if (socDate == default)
        {
            throw new ArgumentOutOfRangeException(nameof(socDate), "SOC date is required.");
        }

        if (intakeDate.HasValue && intakeDate.Value > socDate)
        {
            throw new ArgumentException("Intake date cannot be after the SOC date.", nameof(intakeDate));
        }

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (status == ClientStatus.Discharged && !dischargeDate.HasValue)
        {
            throw new ArgumentException("A discharge date is required for a discharged client.", nameof(dischargeDate));
        }

        if (dischargeDate.HasValue && dischargeDate.Value < socDate)
        {
            throw new ArgumentException("Discharge date cannot be before the SOC date.", nameof(dischargeDate));
        }

        IntakeDate = intakeDate;
        SocDate = socDate;
        Status = status;
        DischargeDate = dischargeDate;
        DischargeReason = Normalize(dischargeReason, MaximumTextLength, nameof(dischargeReason));
    }

    private void Touch(DateTimeOffset updatedAtUtc) =>
        UpdatedAtUtc = EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));

    private static DateTimeOffset EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The timestamp must be UTC.", parameterName);
        }

        return value;
    }

    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Normalize(value, maxLength, parameterName);
    }

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
