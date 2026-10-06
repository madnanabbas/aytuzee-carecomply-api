namespace CareComply.Application.Abstractions.Employees;

public sealed record EmployeePreviousAddress(string Street, string PostalCode, string City, string County, string State);
public sealed record EmployeePayerAffiliation(string Company, string Payer, string PayerId,
    DateOnly? ApplicationFaxedDate, DateOnly? AffiliationDate, DateOnly? AffiliationEndDate);
public sealed record EmployeeBackgroundCheck(DateOnly? CheckDate, DateOnly? ReceivedDate,
    string LicenseType, string StudyType, bool Received);
public sealed record EmployeeBackgroundInformation(IReadOnlyList<EmployeeBackgroundCheck> Checks,
    DateOnly? MantouxGivenDate, DateOnly? MantouxCheckedDate, bool MantouxReceived,
    DateOnly? MedicalTestingDate, bool MedicalTestingReceived);
public sealed record EmployeeCourse(string CourseName, decimal? QualifyingPoints, DateOnly? CompletedOn,
    decimal? ObtainedPoints, string Notes);
public sealed record EmployeeTrainingSkill(string TrainingName, string JobTitle, decimal? QualifyingPoints,
    DateOnly? CompletedOn, DateOnly? ExpiresOn, decimal? ObtainedPoints, string Notes, bool AllowedTraining);
public sealed record EmployeeTrainingInformation(IReadOnlyList<EmployeeCourse> Courses,
    IReadOnlyList<EmployeeTrainingSkill> Trainings);
public sealed record EmployeeContact(string Name, string Phone, string Relationship);

public sealed record EmployeeListItemResponse(Guid Id, Guid? CompanyId, string FirstName, string LastName, string PreferredName,
    string Email, string Phone, string JobTitle, bool IsActive, DateOnly? HireDate, IReadOnlyList<string> Locations);

public sealed record EmployeeListResponse(
    IReadOnlyList<EmployeeListItemResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record EmployeeDetailsResponse(Guid Id, Guid CompanyId, IReadOnlyList<Guid> LocationIds,
    string FirstName, string MiddleName, string LastName, string PreferredName, string Email, string Phone,
    string SecondaryPhone, string JobTitle, string SecondaryJobTitle, bool IsActive, DateOnly? HireDate,
    DateOnly? StartOfServiceDate, DateOnly? InactiveDate, DateOnly? DateOfBirth, string Gender, bool HasSsn,
    IReadOnlyList<string> Languages, decimal? HoursPerMonth, string Umpi, string Npi,
    string LicenseId, DateOnly? DriverLicenseExpiresOn, string StreetAddress, string Apartment, string City,
    string State, string PostalCode, string County, string Notes, string CareProgram, bool HasUsedDifferentName,
    string PreviousName, decimal? HeightInches, decimal? WeightPounds, string HairColor, string EyeColor,
    string BirthPlace, string CfssCertificationNumber, bool HasWorkingHourRestrictions,
    IReadOnlyList<string> AvailableDays, TimeOnly? AvailableStartTime, TimeOnly? AvailableEndTime,
    IReadOnlyList<EmployeePreviousAddress> PreviousAddresses, string MaritalStatus, string Ethnicity,
    IReadOnlyList<string> Qualifications, bool CanWorkWithSmokers, string SmokerRestrictionExplanation,
    bool IsUnionMember, bool IsMilitaryVeteran, string MilitaryServiceExplanation, string Race,
    string DemographicDescription, bool CanWorkWithPets, string PetRestrictionExplanation,
    IReadOnlyList<string> ClientTypes, bool HasOtherJob, string OtherEmployerName,
    string OtherEmploymentDuration, bool CanDriveForWork, string PreviousWorkExperience, string EmergencyContactName,
    string EmergencyContactRelationship, string EmergencyContactPhone, string EmergencyContactEmail,
    string EmergencyContactAddress, string TrainingName, string TrainingProvider, DateOnly? TrainingCompletedOn,
    DateOnly? TrainingExpiresOn, string TrainingCertificateNumber, string PayerName, string PayerMemberId,
    string PayerGroupNumber, string PayerPlanName, DateOnly? PayerEffectiveFrom, DateOnly? PayerEffectiveTo,
    string PayerPhone, bool InsuranceAffiliationRequired, IReadOnlyList<EmployeePayerAffiliation> PayerAffiliations,
    EmployeeBackgroundInformation BackgroundInformation,
    EmployeeTrainingInformation TrainingInformation,
    IReadOnlyList<EmployeeContact> Contacts,
    bool AllowAggregator, bool OverrideRestriction, bool EvvOnly, bool RestrictDateAndTime,
    string EvvEmail, bool HasEvvPassword, bool HasIvrPin, string EvvIvrAuthorization, string EmployeeTier,
    DateOnly? TierEffectiveDate, bool IsOnboarding, bool AllowVisitScreen, bool AllowTravelTime,
    bool AllowManualEntry, bool AllowOneTimeLogin, string EditorVersion);

public sealed record SaveEmployeeRequest(Guid? CompanyId, IReadOnlyList<Guid>? LocationIds, string FirstName,
    string? MiddleName, string LastName, string? PreferredName, string Email, string? Phone,
    string? SecondaryPhone, string JobTitle, string? SecondaryJobTitle, bool IsActive, DateOnly? HireDate,
    DateOnly? StartOfServiceDate, DateOnly? InactiveDate, DateOnly? DateOfBirth, string? Gender, string? Ssn,
    IReadOnlyList<string>? Languages, decimal? HoursPerMonth, string? Umpi, string? Npi,
    string? LicenseId, DateOnly? DriverLicenseExpiresOn, string? StreetAddress, string? Apartment, string? City,
    string? State, string? PostalCode, string? County, string? Notes, string? CareProgram,
    bool HasUsedDifferentName, string? PreviousName, decimal? HeightInches, decimal? WeightPounds,
    string? HairColor, string? EyeColor, string? BirthPlace, string? CfssCertificationNumber,
    bool HasWorkingHourRestrictions, IReadOnlyList<string>? AvailableDays, TimeOnly? AvailableStartTime,
    TimeOnly? AvailableEndTime, IReadOnlyList<EmployeePreviousAddress>? PreviousAddresses,
    string? MaritalStatus, string? Ethnicity, IReadOnlyList<string>? Qualifications, bool CanWorkWithSmokers,
    string? SmokerRestrictionExplanation, bool IsUnionMember, bool IsMilitaryVeteran,
    string? MilitaryServiceExplanation, string? Race, string? DemographicDescription, bool CanWorkWithPets,
    string? PetRestrictionExplanation, IReadOnlyList<string>? ClientTypes, bool HasOtherJob,
    string? OtherEmployerName, string? OtherEmploymentDuration, bool CanDriveForWork,
    string? PreviousWorkExperience, string? EmergencyContactName,
    string? EmergencyContactRelationship, string? EmergencyContactPhone, string? EmergencyContactEmail,
    string? EmergencyContactAddress, string? TrainingName, string? TrainingProvider, DateOnly? TrainingCompletedOn,
    DateOnly? TrainingExpiresOn, string? TrainingCertificateNumber, string? PayerName, string? PayerMemberId,
    string? PayerGroupNumber, string? PayerPlanName, DateOnly? PayerEffectiveFrom, DateOnly? PayerEffectiveTo,
    string? PayerPhone, bool InsuranceAffiliationRequired, IReadOnlyList<EmployeePayerAffiliation>? PayerAffiliations,
    EmployeeBackgroundInformation? BackgroundInformation,
    EmployeeTrainingInformation? TrainingInformation,
    IReadOnlyList<EmployeeContact>? Contacts,
    bool? AllowAggregator, bool? OverrideRestriction, bool? EvvOnly, bool? RestrictDateAndTime,
    string? EvvEmail, string? EvvPassword, string? IvrPin, string? EvvIvrAuthorization, string? EmployeeTier,
    DateOnly? TierEffectiveDate, bool IsOnboarding, bool AllowVisitScreen, bool AllowTravelTime,
    bool AllowManualEntry, bool AllowOneTimeLogin, string? EditorVersion = null);
