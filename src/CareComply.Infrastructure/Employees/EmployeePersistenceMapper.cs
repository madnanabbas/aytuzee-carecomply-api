using System.Text.Json;
using CareComply.Application.Abstractions.Employees;
using CareComply.Application.Features.Employees;
using CareComply.Infrastructure.Identity;

namespace CareComply.Infrastructure.Employees;

internal sealed class EmployeePersistenceMapper(
    IEmployeeSensitiveData sensitiveData,
    TimeProvider timeProvider)
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    public void Apply(ApplicationUser employee, SaveEmployeeRequest request)
    {
        ApplyIdentityAndEmployment(employee, request);
        ApplyAddressAndAdditionalInformation(employee, request);
        ApplyPayerInformation(employee, request);
        ApplyBackgroundInformation(employee, request);
        ApplyTrainingInformation(employee, request);
        ApplyContacts(employee, request);
        ApplyEvvAndAccessSettings(employee, request);
        employee.UpdatedAtUtc = UtcNow;
    }

    private void ApplyIdentityAndEmployment(ApplicationUser employee, SaveEmployeeRequest request)
    {
        employee.FirstName = request.FirstName.Trim();
        employee.MiddleName = Clean(request.MiddleName);
        employee.LastName = request.LastName.Trim();
        employee.PreferredName = Clean(request.PreferredName);
        employee.Email = request.Email.Trim();
        employee.PhoneNumber = Clean(request.Phone);
        employee.SecondaryPhone = Clean(request.SecondaryPhone);
        employee.JobTitle = request.JobTitle.Trim();
        employee.SecondaryJobTitle = Clean(request.SecondaryJobTitle);
        employee.IsActive = request.IsActive;
        employee.HireDate = request.HireDate;
        employee.StartOfServiceDate = request.StartOfServiceDate;
        employee.InactiveDate = request.InactiveDate;
        employee.DateOfBirth = request.DateOfBirth;
        employee.Gender = Clean(request.Gender);
        employee.HoursPerMonth = request.HoursPerMonth;
        employee.Umpi = Clean(request.Umpi);
        employee.Npi = Clean(request.Npi);
        employee.LicenseId = Clean(request.LicenseId);
        employee.DriverLicenseExpiresOn = request.DriverLicenseExpiresOn;
        employee.LanguagesCsv = Csv(request.Languages);

        if (!string.IsNullOrWhiteSpace(request.Ssn))
        {
            employee.SsnProtected = sensitiveData.ProtectSsn(
                request.Ssn.Replace("-", "", StringComparison.Ordinal));
        }
    }

    private static void ApplyAddressAndAdditionalInformation(
        ApplicationUser employee,
        SaveEmployeeRequest request)
    {
        employee.StreetAddress = Clean(request.StreetAddress);
        employee.Apartment = Clean(request.Apartment);
        employee.City = Clean(request.City);
        employee.State = Clean(request.State);
        employee.PostalCode = Clean(request.PostalCode);
        employee.County = Clean(request.County);
        employee.EmployeeNotes = Clean(request.Notes);
        employee.CareProgram = Clean(request.CareProgram);
        employee.HasUsedDifferentName = request.HasUsedDifferentName;
        employee.PreviousName = request.HasUsedDifferentName ? Clean(request.PreviousName) : string.Empty;
        employee.HeightInches = request.HeightInches;
        employee.WeightPounds = request.WeightPounds;
        employee.HairColor = Clean(request.HairColor);
        employee.EyeColor = Clean(request.EyeColor);
        employee.BirthPlace = Clean(request.BirthPlace);
        employee.CfssCertificationNumber = Clean(request.CfssCertificationNumber);
        employee.HasWorkingHourRestrictions = request.HasWorkingHourRestrictions;
        employee.AvailableDaysCsv = Csv(request.AvailableDays);
        employee.AvailableStartTime = request.HasWorkingHourRestrictions ? request.AvailableStartTime : null;
        employee.AvailableEndTime = request.HasWorkingHourRestrictions ? request.AvailableEndTime : null;
        employee.PreviousAddressesJson = JsonSerializer.Serialize(
            (request.PreviousAddresses ?? [])
                .Select(address => new EmployeePreviousAddress(
                    Clean(address.Street),
                    Clean(address.PostalCode),
                    Clean(address.City),
                    Clean(address.County),
                    Clean(address.State)))
                .Where(address => address.Street.Length > 0 || address.City.Length > 0));
        employee.MaritalStatus = Clean(request.MaritalStatus);
        employee.Ethnicity = Clean(request.Ethnicity);
        employee.QualificationsCsv = Csv(request.Qualifications);
        employee.CanWorkWithSmokers = request.CanWorkWithSmokers;
        employee.SmokerRestrictionExplanation = request.CanWorkWithSmokers
            ? string.Empty
            : Clean(request.SmokerRestrictionExplanation);
        employee.IsUnionMember = request.IsUnionMember;
        employee.IsMilitaryVeteran = request.IsMilitaryVeteran;
        employee.MilitaryServiceExplanation = request.IsMilitaryVeteran
            ? Clean(request.MilitaryServiceExplanation)
            : string.Empty;
        employee.Race = Clean(request.Race);
        employee.DemographicDescription = Clean(request.DemographicDescription);
        employee.CanWorkWithPets = request.CanWorkWithPets;
        employee.PetRestrictionExplanation = request.CanWorkWithPets
            ? string.Empty
            : Clean(request.PetRestrictionExplanation);
        employee.ClientTypesCsv = Csv(request.ClientTypes);
        employee.HasOtherJob = request.HasOtherJob;
        employee.OtherEmployerName = request.HasOtherJob ? Clean(request.OtherEmployerName) : string.Empty;
        employee.OtherEmploymentDuration = request.HasOtherJob
            ? Clean(request.OtherEmploymentDuration)
            : string.Empty;
        employee.CanDriveForWork = request.CanDriveForWork;
        employee.PreviousWorkExperience = Clean(request.PreviousWorkExperience);
    }

    private static void ApplyPayerInformation(ApplicationUser employee, SaveEmployeeRequest request)
    {
        employee.PayerName = Clean(request.PayerName);
        employee.PayerMemberId = Clean(request.PayerMemberId);
        employee.PayerGroupNumber = Clean(request.PayerGroupNumber);
        employee.PayerPlanName = Clean(request.PayerPlanName);
        employee.PayerEffectiveFrom = request.PayerEffectiveFrom;
        employee.PayerEffectiveTo = request.PayerEffectiveTo;
        employee.PayerPhone = Clean(request.PayerPhone);
        employee.InsuranceAffiliationRequired = request.InsuranceAffiliationRequired;
        employee.PayerAffiliationsJson = JsonSerializer.Serialize(
            (request.PayerAffiliations ?? []).Select(affiliation => new EmployeePayerAffiliation(
                Clean(affiliation.Company),
                Clean(affiliation.Payer),
                Clean(affiliation.PayerId),
                affiliation.ApplicationFaxedDate,
                affiliation.AffiliationDate,
                affiliation.AffiliationEndDate)));

        if (request.PayerAffiliations is { Count: > 0 })
        {
            var primaryAffiliation = request.PayerAffiliations[0];
            employee.PayerName = Clean(primaryAffiliation.Payer);
            employee.PayerMemberId = Clean(primaryAffiliation.PayerId);
            employee.PayerEffectiveFrom = primaryAffiliation.AffiliationDate;
            employee.PayerEffectiveTo = primaryAffiliation.AffiliationEndDate;
        }
    }

    private static void ApplyBackgroundInformation(ApplicationUser employee, SaveEmployeeRequest request)
    {
        var background = request.BackgroundInformation ?? EmptyBackgroundInformation();
        employee.EmployeeBackgroundInformationJson = JsonSerializer.Serialize(
            new EmployeeBackgroundInformation(
                (background.Checks ?? []).Select(check => new EmployeeBackgroundCheck(
                    check.CheckDate,
                    check.ReceivedDate,
                    Clean(check.LicenseType),
                    Clean(check.StudyType),
                    check.Received)).ToArray(),
                background.MantouxGivenDate,
                background.MantouxCheckedDate,
                background.MantouxReceived,
                background.MedicalTestingDate,
                background.MedicalTestingReceived));
    }

    private static void ApplyTrainingInformation(ApplicationUser employee, SaveEmployeeRequest request)
    {
        employee.TrainingName = Clean(request.TrainingName);
        employee.TrainingProvider = Clean(request.TrainingProvider);
        employee.TrainingCompletedOn = request.TrainingCompletedOn;
        employee.TrainingExpiresOn = request.TrainingExpiresOn;
        employee.TrainingCertificateNumber = Clean(request.TrainingCertificateNumber);

        var training = request.TrainingInformation ?? EmptyTrainingInformation();
        employee.EmployeeTrainingInformationJson = JsonSerializer.Serialize(
            new EmployeeTrainingInformation(
                (training.Courses ?? []).Select(course => new EmployeeCourse(
                    Clean(course.CourseName),
                    course.QualifyingPoints,
                    course.CompletedOn,
                    course.ObtainedPoints,
                    Clean(course.Notes))).ToArray(),
                (training.Trainings ?? []).Select(item => new EmployeeTrainingSkill(
                    Clean(item.TrainingName),
                    Clean(item.JobTitle),
                    item.QualifyingPoints,
                    item.CompletedOn,
                    item.ExpiresOn,
                    item.ObtainedPoints,
                    Clean(item.Notes),
                    item.AllowedTraining)).ToArray()));

        if (training.Trainings is { Count: > 0 })
        {
            var primaryTraining = training.Trainings[0];
            employee.TrainingName = Clean(primaryTraining.TrainingName);
            employee.TrainingProvider = Clean(primaryTraining.JobTitle);
            employee.TrainingCompletedOn = primaryTraining.CompletedOn;
            employee.TrainingExpiresOn = primaryTraining.ExpiresOn;
        }
    }

    private static void ApplyContacts(ApplicationUser employee, SaveEmployeeRequest request)
    {
        employee.EmergencyContactName = Clean(request.EmergencyContactName);
        employee.EmergencyContactRelationship = Clean(request.EmergencyContactRelationship);
        employee.EmergencyContactPhone = Clean(request.EmergencyContactPhone);
        employee.EmergencyContactEmail = Clean(request.EmergencyContactEmail);
        employee.EmergencyContactAddress = Clean(request.EmergencyContactAddress);

        var contacts = (request.Contacts ?? [])
            .Select(contact => new EmployeeContact(
                Clean(contact.Name),
                Clean(contact.Phone),
                Clean(contact.Relationship)))
            .ToArray();
        employee.EmployeeContactsJson = JsonSerializer.Serialize(contacts);

        if (contacts.Length > 0)
        {
            employee.EmergencyContactName = contacts[0].Name;
            employee.EmergencyContactPhone = contacts[0].Phone;
            employee.EmergencyContactRelationship = contacts[0].Relationship;
        }
        else if (request.Contacts is not null)
        {
            employee.EmergencyContactName = string.Empty;
            employee.EmergencyContactPhone = string.Empty;
            employee.EmergencyContactRelationship = string.Empty;
            employee.EmergencyContactEmail = string.Empty;
            employee.EmergencyContactAddress = string.Empty;
        }
    }

    private void ApplyEvvAndAccessSettings(ApplicationUser employee, SaveEmployeeRequest request)
    {
        employee.AllowAggregator = request.AllowAggregator;
        employee.OverrideRestriction = request.OverrideRestriction;
        employee.EvvOnly = request.EvvOnly;
        employee.RestrictDateAndTime = request.RestrictDateAndTime;
        employee.EvvEmail = Clean(request.EvvEmail);
        if (!string.IsNullOrWhiteSpace(request.EvvPassword))
        {
            employee.EvvPasswordProtected = sensitiveData.ProtectWorkforceSecret(request.EvvPassword);
        }

        if (!string.IsNullOrWhiteSpace(request.IvrPin))
        {
            employee.IvrPinProtected = sensitiveData.ProtectWorkforceSecret(request.IvrPin);
        }

        employee.EvvIvrAuthorization = Clean(request.EvvIvrAuthorization);
        employee.EmployeeTier = Clean(request.EmployeeTier);
        employee.TierEffectiveDate = request.TierEffectiveDate;
        employee.IsOnboarding = request.IsOnboarding;
        employee.AllowVisitScreen = request.AllowVisitScreen;
        employee.AllowTravelTime = request.AllowTravelTime;
        employee.AllowManualEntry = request.AllowManualEntry;
        employee.AllowOneTimeLogin = request.AllowOneTimeLogin;
    }

    private static string Clean(string? value) => value?.Trim() ?? string.Empty;

    private static string Csv(IEnumerable<string>? values) => string.Join(
        ',',
        values?
            .Select(Clean)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
        ?? []);

    private static EmployeeBackgroundInformation EmptyBackgroundInformation() =>
        new([], null, null, false, null, false);

    private static EmployeeTrainingInformation EmptyTrainingInformation() => new([], []);
}
