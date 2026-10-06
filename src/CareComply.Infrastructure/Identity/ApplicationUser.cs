using Microsoft.AspNetCore.Identity;
using CareComply.Application.Abstractions.Identity;

namespace CareComply.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        Id = Guid.CreateVersion7();
    }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public Guid? CompanyId { get; set; }

    public bool IsPlatformAdministrator { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsEmployee { get; set; }

    public string JobTitle { get; set; } = string.Empty;

    // Workforce profile. Employees remain identity records so assignments, auditing and
    // optional future portal access all reference one stable person identifier.
    public string MiddleName { get; set; } = string.Empty;
    public string PreferredName { get; set; } = string.Empty;
    public string SecondaryJobTitle { get; set; } = string.Empty;
    public DateOnly? HireDate { get; set; }
    public DateOnly? StartOfServiceDate { get; set; }
    public DateOnly? InactiveDate { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string SsnProtected { get; set; } = string.Empty;
    public string SecondaryPhone { get; set; } = string.Empty;
    public string LanguagesCsv { get; set; } = string.Empty;
    public decimal? HoursPerMonth { get; set; }
    public Guid? SupervisorUserId { get; set; }
    public string Umpi { get; set; } = string.Empty;
    public string Npi { get; set; } = string.Empty;
    public string LicenseId { get; set; } = string.Empty;
    public DateOnly? DriverLicenseExpiresOn { get; set; }
    public string StreetAddress { get; set; } = string.Empty;
    public string Apartment { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string County { get; set; } = string.Empty;
    public string EmployeeNotes { get; set; } = string.Empty;
    public string CareProgram { get; set; } = string.Empty;
    public bool HasUsedDifferentName { get; set; }
    public string PreviousName { get; set; } = string.Empty;
    public decimal? HeightInches { get; set; }
    public decimal? WeightPounds { get; set; }
    public string HairColor { get; set; } = string.Empty;
    public string EyeColor { get; set; } = string.Empty;
    public string BirthPlace { get; set; } = string.Empty;
    public string CfssCertificationNumber { get; set; } = string.Empty;
    public bool HasWorkingHourRestrictions { get; set; }
    public string AvailableDaysCsv { get; set; } = string.Empty;
    public TimeOnly? AvailableStartTime { get; set; }
    public TimeOnly? AvailableEndTime { get; set; }
    public string PreviousAddressesJson { get; set; } = "[]";
    public string MaritalStatus { get; set; } = string.Empty;
    public string Ethnicity { get; set; } = string.Empty;
    public string QualificationsCsv { get; set; } = string.Empty;
    public bool CanWorkWithSmokers { get; set; }
    public string SmokerRestrictionExplanation { get; set; } = string.Empty;
    public bool IsUnionMember { get; set; }
    public bool IsMilitaryVeteran { get; set; }
    public string MilitaryServiceExplanation { get; set; } = string.Empty;
    public string Race { get; set; } = string.Empty;
    public string DemographicDescription { get; set; } = string.Empty;
    public bool CanWorkWithPets { get; set; }
    public string PetRestrictionExplanation { get; set; } = string.Empty;
    public string ClientTypesCsv { get; set; } = string.Empty;
    public bool HasOtherJob { get; set; }
    public string OtherEmployerName { get; set; } = string.Empty;
    public string OtherEmploymentDuration { get; set; } = string.Empty;
    public bool CanDriveForWork { get; set; }
    public string PreviousWorkExperience { get; set; } = string.Empty;
    public string EmergencyContactName { get; set; } = string.Empty;
    public string EmergencyContactRelationship { get; set; } = string.Empty;
    public string EmergencyContactPhone { get; set; } = string.Empty;
    public string EmergencyContactEmail { get; set; } = string.Empty;
    public string EmergencyContactAddress { get; set; } = string.Empty;
    public string TrainingName { get; set; } = string.Empty;
    public string TrainingProvider { get; set; } = string.Empty;
    public DateOnly? TrainingCompletedOn { get; set; }
    public DateOnly? TrainingExpiresOn { get; set; }
    public string TrainingCertificateNumber { get; set; } = string.Empty;
    public string PayerName { get; set; } = string.Empty;
    public string PayerMemberId { get; set; } = string.Empty;
    public string PayerGroupNumber { get; set; } = string.Empty;
    public string PayerPlanName { get; set; } = string.Empty;
    public DateOnly? PayerEffectiveFrom { get; set; }
    public DateOnly? PayerEffectiveTo { get; set; }
    public string PayerPhone { get; set; } = string.Empty;
    public bool InsuranceAffiliationRequired { get; set; }
    public string PayerAffiliationsJson { get; set; } = "[]";
    public string EmployeeBackgroundInformationJson { get; set; } = "{}";
    public string EmployeeTrainingInformationJson { get; set; } = "{}";
    public string EmployeeContactsJson { get; set; } = "[]";
    public bool AllowAggregator { get; set; }
    public bool OverrideRestriction { get; set; }
    public bool EvvOnly { get; set; }
    public bool RestrictDateAndTime { get; set; }
    public string EvvEmail { get; set; } = string.Empty;
    public string EvvPasswordProtected { get; set; } = string.Empty;
    public string IvrPinProtected { get; set; } = string.Empty;
    public string EvvIvrAuthorization { get; set; } = string.Empty;
    public string EmployeeTier { get; set; } = string.Empty;
    public DateOnly? TierEffectiveDate { get; set; }
    public bool IsOnboarding { get; set; }
    public bool AllowVisitScreen { get; set; }
    public bool AllowTravelTime { get; set; }
    public bool AllowManualEntry { get; set; }
    public bool AllowOneTimeLogin { get; set; }

    public bool RequiresMfa { get; set; }

    public UserMfaMethod MfaMethod { get; set; } = UserMfaMethod.Authenticator;

    public DateTimeOffset? LastLoginAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
