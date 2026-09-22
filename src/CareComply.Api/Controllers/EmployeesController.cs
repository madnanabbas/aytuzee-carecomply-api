using CareComply.Api.Authorization;
using CareComply.Application.Abstractions.Employees;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Application.Authorization;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CareComply.Api.Controllers;

[ApiController, Route("api/employees")]
public sealed class EmployeesController(CareComplyDbContext db, ITenantScopeAccessor tenants,
    UserManager<ApplicationUser> users, IDataProtectionProvider protection, TimeProvider clock,
    IEmployeeDirectory employeeDirectory, IEmployeeDetailsReader employeeDetails) : ControllerBase
{
    private readonly IDataProtector ssn = protection.CreateProtector("CareComply.Employee.SSN.v1");
    private readonly IDataProtector workforceSecrets = protection.CreateProtector("CareComply.Employee.EVV.v1");

    [HttpGet, HasPermission(Permissions.Employee.View)]
    public async Task<ActionResult<IReadOnlyList<EmployeeListItemResponse>>> List([FromQuery] string? search, CancellationToken ct)
    {
        var employees = await employeeDirectory.ListAsync(search, ct);
        if (employees is null) return Forbid();

        return Ok(employees.Select(employee => new EmployeeListItemResponse(
            employee.Id, employee.CompanyId, employee.FirstName, employee.LastName, employee.PreferredName,
            employee.Email, employee.Phone, employee.JobTitle, employee.IsActive,
            employee.HireDate, employee.Locations)).ToArray());
    }

    [HttpGet("{id:guid}"), HasPermission(Permissions.Employee.View)]
    public async Task<ActionResult<EmployeeDetailsResponse>> Get(Guid id, CancellationToken ct)
    {
        var employee = await employeeDetails.GetAsync(id, ct);
        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpPost, HasPermission(Permissions.Employee.Create)]
    public async Task<ActionResult<EmployeeDetailsResponse>> Create(SaveEmployeeRequest request, CancellationToken ct)
    {
        var tenant = await tenants.GetAsync(ct); if (tenant is null) return Unauthorized();
        var companyId = tenant.IsPlatformAdministrator ? request.CompanyId : tenant.CompanyId;
        if (companyId is null || !await Valid(request, companyId.Value, ct)) return ValidationProblem(ModelState);
        var employee = new ApplicationUser { CompanyId = companyId, IsEmployee = true, CreatedAtUtc = clock.GetUtcNow() };
        Apply(employee, request); employee.UserName = employee.Email; employee.EmailConfirmed = true;
        var result = await users.CreateAsync(employee); if (!result.Succeeded) { foreach (var e in result.Errors) ModelState.AddModelError(e.Code, e.Description); return ValidationProblem(ModelState); }
        await ReplaceLocations(employee.Id, request.LocationIds!, ct); await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = employee.Id }, (await employeeDetails.GetAsync(employee.Id, ct))!);
    }

    [HttpPut("{id:guid}"), HasPermission(Permissions.Employee.Edit)]
    public async Task<ActionResult<EmployeeDetailsResponse>> Update(Guid id, SaveEmployeeRequest request, CancellationToken ct)
    {
        var query = await Scoped(ct); var employee = query is null ? null : await query.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (employee is null) return NotFound();
        if (!await Valid(request, employee.CompanyId!.Value, ct, id)) return ValidationProblem(ModelState);
        Apply(employee, request); employee.NormalizedEmail = users.NormalizeEmail(employee.Email); employee.NormalizedUserName = users.NormalizeName(employee.Email); employee.UserName = employee.Email;
        await ReplaceLocations(id, request.LocationIds!, ct); await db.SaveChangesAsync(ct); return Ok(await employeeDetails.GetAsync(id, ct));
    }

    private async Task<IQueryable<ApplicationUser>?> Scoped(CancellationToken ct)
    {
        var tenant = await tenants.GetAsync(ct); if (tenant is null) return null;
        var query = db.Users.Where(x => x.IsEmployee);
        if (!tenant.IsPlatformAdministrator) query = query.Where(x => x.CompanyId == tenant.CompanyId);
        return query;
    }

    private async Task<bool> Valid(SaveEmployeeRequest r, Guid companyId, CancellationToken ct, Guid? currentId = null)
    {
        if (string.IsNullOrWhiteSpace(r.FirstName)) ModelState.AddModelError(nameof(r.FirstName), "First name is required.");
        if (string.IsNullOrWhiteSpace(r.LastName)) ModelState.AddModelError(nameof(r.LastName), "Last name is required.");
        if (string.IsNullOrWhiteSpace(r.Email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(r.Email)) ModelState.AddModelError(nameof(r.Email), "A valid email is required.");
        if (string.IsNullOrWhiteSpace(r.JobTitle)) ModelState.AddModelError(nameof(r.JobTitle), "Title is required.");
        if (r.HireDate is null) ModelState.AddModelError(nameof(r.HireDate), "Hire date is required.");
        if (r.DateOfBirth is null) ModelState.AddModelError(nameof(r.DateOfBirth), "Date of birth is required.");
        if (string.IsNullOrWhiteSpace(r.Gender)) ModelState.AddModelError(nameof(r.Gender), "Gender is required.");
        if (string.IsNullOrWhiteSpace(r.Phone) || r.Phone.Count(char.IsDigit) != 10) ModelState.AddModelError(nameof(r.Phone), "Enter a 10-digit phone number.");
        if (string.IsNullOrWhiteSpace(r.StreetAddress)) ModelState.AddModelError(nameof(r.StreetAddress), "Street address is required.");
        if (currentId is null && string.IsNullOrWhiteSpace(r.Ssn)) ModelState.AddModelError(nameof(r.Ssn), "SSN is required when creating an employee.");
        if (!string.IsNullOrWhiteSpace(r.Ssn) && r.Ssn.Count(char.IsDigit) != 9) ModelState.AddModelError(nameof(r.Ssn), "SSN must contain 9 digits.");
        if (!string.IsNullOrWhiteSpace(r.EvvEmail) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(r.EvvEmail)) ModelState.AddModelError(nameof(r.EvvEmail), "Enter a valid EVV email.");
        if (r.LocationIds is not { Count: > 0 } || await db.CompanyLocations.CountAsync(x => r.LocationIds.Contains(x.Id) && x.CompanyId == companyId && x.IsActive, ct) != r.LocationIds.Distinct().Count()) ModelState.AddModelError(nameof(r.LocationIds), "Select at least one valid company location.");
        if (await db.Users.AnyAsync(x => x.Id != currentId && x.NormalizedEmail == users.NormalizeEmail(r.Email), ct)) ModelState.AddModelError(nameof(r.Email), "Email is already in use.");
        if (!string.IsNullOrWhiteSpace(r.Npi) && (!r.Npi.All(char.IsDigit) || r.Npi.Length != 10)) ModelState.AddModelError(nameof(r.Npi), "NPI must contain 10 digits.");
        if (r.HasUsedDifferentName && string.IsNullOrWhiteSpace(r.PreviousName)) ModelState.AddModelError(nameof(r.PreviousName), "Previous name is required when a different name was used.");
        if (r.HeightInches is <= 0 or > 120) ModelState.AddModelError(nameof(r.HeightInches), "Height must be between 1 and 120 inches.");
        if (r.WeightPounds is <= 0 or > 1500) ModelState.AddModelError(nameof(r.WeightPounds), "Weight must be between 1 and 1,500 pounds.");
        if (r.HasWorkingHourRestrictions && (r.AvailableStartTime is null || r.AvailableEndTime is null)) ModelState.AddModelError(nameof(r.AvailableStartTime), "Start and end times are required when working hours are restricted.");
        if (r.AvailableStartTime is not null && r.AvailableEndTime is not null && r.AvailableStartTime >= r.AvailableEndTime) ModelState.AddModelError(nameof(r.AvailableEndTime), "End time must be after start time.");
        if (!r.CanWorkWithSmokers && string.IsNullOrWhiteSpace(r.SmokerRestrictionExplanation)) ModelState.AddModelError(nameof(r.SmokerRestrictionExplanation), "Explain the smoking restriction.");
        if (r.IsMilitaryVeteran && string.IsNullOrWhiteSpace(r.MilitaryServiceExplanation)) ModelState.AddModelError(nameof(r.MilitaryServiceExplanation), "Provide military service details.");
        if (!r.CanWorkWithPets && string.IsNullOrWhiteSpace(r.PetRestrictionExplanation)) ModelState.AddModelError(nameof(r.PetRestrictionExplanation), "Explain the pet restriction.");
        if (r.HasOtherJob && string.IsNullOrWhiteSpace(r.OtherEmployerName)) ModelState.AddModelError(nameof(r.OtherEmployerName), "Employer name is required when the employee has another job.");
        if (r.PreviousAddresses?.Count > 10) ModelState.AddModelError(nameof(r.PreviousAddresses), "No more than 10 previous addresses may be recorded.");
        if (r.PayerAffiliations?.Count > 20) ModelState.AddModelError(nameof(r.PayerAffiliations), "No more than 20 payer affiliations may be recorded.");
        if (r.InsuranceAffiliationRequired && r.PayerAffiliations is not { Count: > 0 }) ModelState.AddModelError(nameof(r.PayerAffiliations), "Add at least one payer affiliation when insurance affiliation is required.");
        foreach (var affiliation in r.PayerAffiliations ?? [])
        {
            if (string.IsNullOrWhiteSpace(affiliation.Company) || string.IsNullOrWhiteSpace(affiliation.Payer)) ModelState.AddModelError(nameof(r.PayerAffiliations), "Company and payer are required for every affiliation.");
            if (affiliation.AffiliationDate is not null && affiliation.AffiliationEndDate is not null && affiliation.AffiliationEndDate < affiliation.AffiliationDate) ModelState.AddModelError(nameof(r.PayerAffiliations), "Affiliation end date cannot be before the affiliation date.");
        }
        var background = r.BackgroundInformation;
        if (background?.Checks?.Count > 20) ModelState.AddModelError(nameof(r.BackgroundInformation), "No more than 20 background checks may be recorded.");
        foreach (var check in background?.Checks ?? [])
        {
            if (string.IsNullOrWhiteSpace(check.LicenseType) || string.IsNullOrWhiteSpace(check.StudyType)) ModelState.AddModelError(nameof(r.BackgroundInformation), "License type and study type are required for every background check.");
            if (check.LicenseType?.Length > 100 || check.StudyType?.Length > 100) ModelState.AddModelError(nameof(r.BackgroundInformation), "Background check license and study types cannot exceed 100 characters.");
            if (check.Received && check.ReceivedDate is null) ModelState.AddModelError(nameof(r.BackgroundInformation), "Received date is required when a background check is marked received.");
            if (check.CheckDate is not null && check.ReceivedDate is not null && check.ReceivedDate < check.CheckDate) ModelState.AddModelError(nameof(r.BackgroundInformation), "Background check received date cannot be before its check date.");
        }
        if (background?.MantouxGivenDate is not null && background.MantouxCheckedDate is not null && background.MantouxCheckedDate < background.MantouxGivenDate) ModelState.AddModelError(nameof(r.BackgroundInformation), "Mantoux checked date cannot be before the given date.");
        if (background?.MantouxReceived == true && (background.MantouxGivenDate is null || background.MantouxCheckedDate is null)) ModelState.AddModelError(nameof(r.BackgroundInformation), "Mantoux given and checked dates are required when the result is received.");
        if (background?.MedicalTestingReceived == true && background.MedicalTestingDate is null) ModelState.AddModelError(nameof(r.BackgroundInformation), "Medical testing date is required when the result is received.");
        var training = r.TrainingInformation;
        if (training?.Courses?.Count > 50 || training?.Trainings?.Count > 50) ModelState.AddModelError(nameof(r.TrainingInformation), "No more than 50 courses and 50 training records may be recorded.");
        foreach (var course in training?.Courses ?? [])
        {
            if (string.IsNullOrWhiteSpace(course.CourseName)) ModelState.AddModelError(nameof(r.TrainingInformation), "Course name is required for every course.");
            if (course.CourseName?.Length > 200 || course.Notes?.Length > 1000) ModelState.AddModelError(nameof(r.TrainingInformation), "Course name or notes exceed the allowed length.");
            if (course.QualifyingPoints is < 0 or > 10000 || course.ObtainedPoints is < 0 or > 10000) ModelState.AddModelError(nameof(r.TrainingInformation), "Course points must be between 0 and 10,000.");
        }
        foreach (var item in training?.Trainings ?? [])
        {
            if (string.IsNullOrWhiteSpace(item.TrainingName)) ModelState.AddModelError(nameof(r.TrainingInformation), "Training name is required for every training record.");
            if (item.TrainingName?.Length > 200 || item.JobTitle?.Length > 200 || item.Notes?.Length > 1000) ModelState.AddModelError(nameof(r.TrainingInformation), "Training name, job title or notes exceed the allowed length.");
            if (item.QualifyingPoints is < 0 or > 10000 || item.ObtainedPoints is < 0 or > 10000) ModelState.AddModelError(nameof(r.TrainingInformation), "Training points must be between 0 and 10,000.");
            if (item.CompletedOn is not null && item.ExpiresOn is not null && item.ExpiresOn < item.CompletedOn) ModelState.AddModelError(nameof(r.TrainingInformation), "Training expiration date cannot be before its completion date.");
        }
        if (r.Contacts?.Count > 20) ModelState.AddModelError(nameof(r.Contacts), "No more than 20 employee contacts may be recorded.");
        foreach (var contact in r.Contacts ?? [])
        {
            if (string.IsNullOrWhiteSpace(contact.Name)) ModelState.AddModelError(nameof(r.Contacts), "Contact name is required for every employee contact.");
            if (contact.Name?.Length > 200 || contact.Relationship?.Length > 100) ModelState.AddModelError(nameof(r.Contacts), "Contact name or relationship exceeds the allowed length.");
            if (!string.IsNullOrWhiteSpace(contact.Phone) && contact.Phone.Count(char.IsDigit) != 10) ModelState.AddModelError(nameof(r.Contacts), "Contact phone must contain 10 digits.");
        }
        return ModelState.IsValid;
    }

    private void Apply(ApplicationUser x, SaveEmployeeRequest r)
    {
        x.FirstName = r.FirstName.Trim(); x.MiddleName = Clean(r.MiddleName); x.LastName = r.LastName.Trim(); x.PreferredName = Clean(r.PreferredName);
        x.Email = r.Email.Trim(); x.PhoneNumber = Clean(r.Phone); x.SecondaryPhone = Clean(r.SecondaryPhone); x.JobTitle = r.JobTitle.Trim(); x.SecondaryJobTitle = Clean(r.SecondaryJobTitle);
        x.IsActive = r.IsActive; x.HireDate = r.HireDate; x.StartOfServiceDate = r.StartOfServiceDate; x.InactiveDate = r.InactiveDate; x.DateOfBirth = r.DateOfBirth; x.Gender = Clean(r.Gender);
        if (!string.IsNullOrWhiteSpace(r.Ssn)) x.SsnProtected = ssn.Protect(r.Ssn.Replace("-", "", StringComparison.Ordinal));
        x.LanguagesCsv = string.Join(',', r.Languages?.Select(Clean).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase) ?? []); x.HoursPerMonth = r.HoursPerMonth;
        x.Umpi = Clean(r.Umpi); x.Npi = Clean(r.Npi); x.LicenseId = Clean(r.LicenseId); x.DriverLicenseExpiresOn = r.DriverLicenseExpiresOn;
        x.StreetAddress = Clean(r.StreetAddress); x.Apartment = Clean(r.Apartment); x.City = Clean(r.City); x.State = Clean(r.State); x.PostalCode = Clean(r.PostalCode); x.County = Clean(r.County); x.EmployeeNotes = Clean(r.Notes);
        x.CareProgram = Clean(r.CareProgram); x.HasUsedDifferentName = r.HasUsedDifferentName; x.PreviousName = r.HasUsedDifferentName ? Clean(r.PreviousName) : ""; x.HeightInches = r.HeightInches; x.WeightPounds = r.WeightPounds; x.HairColor = Clean(r.HairColor); x.EyeColor = Clean(r.EyeColor); x.BirthPlace = Clean(r.BirthPlace); x.CfssCertificationNumber = Clean(r.CfssCertificationNumber);
        x.HasWorkingHourRestrictions = r.HasWorkingHourRestrictions; x.AvailableDaysCsv = Csv(r.AvailableDays); x.AvailableStartTime = r.HasWorkingHourRestrictions ? r.AvailableStartTime : null; x.AvailableEndTime = r.HasWorkingHourRestrictions ? r.AvailableEndTime : null; x.PreviousAddressesJson = JsonSerializer.Serialize((r.PreviousAddresses ?? []).Select(a => new EmployeePreviousAddress(Clean(a.Street), Clean(a.PostalCode), Clean(a.City), Clean(a.County), Clean(a.State))).Where(a => a.Street.Length > 0 || a.City.Length > 0));
        x.MaritalStatus = Clean(r.MaritalStatus); x.Ethnicity = Clean(r.Ethnicity); x.QualificationsCsv = Csv(r.Qualifications); x.CanWorkWithSmokers = r.CanWorkWithSmokers; x.SmokerRestrictionExplanation = r.CanWorkWithSmokers ? "" : Clean(r.SmokerRestrictionExplanation); x.IsUnionMember = r.IsUnionMember; x.IsMilitaryVeteran = r.IsMilitaryVeteran; x.MilitaryServiceExplanation = r.IsMilitaryVeteran ? Clean(r.MilitaryServiceExplanation) : ""; x.Race = Clean(r.Race); x.DemographicDescription = Clean(r.DemographicDescription); x.CanWorkWithPets = r.CanWorkWithPets; x.PetRestrictionExplanation = r.CanWorkWithPets ? "" : Clean(r.PetRestrictionExplanation); x.ClientTypesCsv = Csv(r.ClientTypes); x.HasOtherJob = r.HasOtherJob; x.OtherEmployerName = r.HasOtherJob ? Clean(r.OtherEmployerName) : ""; x.OtherEmploymentDuration = r.HasOtherJob ? Clean(r.OtherEmploymentDuration) : ""; x.CanDriveForWork = r.CanDriveForWork; x.PreviousWorkExperience = Clean(r.PreviousWorkExperience);
        x.EmergencyContactName = Clean(r.EmergencyContactName); x.EmergencyContactRelationship = Clean(r.EmergencyContactRelationship); x.EmergencyContactPhone = Clean(r.EmergencyContactPhone); x.EmergencyContactEmail = Clean(r.EmergencyContactEmail); x.EmergencyContactAddress = Clean(r.EmergencyContactAddress);
        x.TrainingName = Clean(r.TrainingName); x.TrainingProvider = Clean(r.TrainingProvider); x.TrainingCompletedOn = r.TrainingCompletedOn; x.TrainingExpiresOn = r.TrainingExpiresOn; x.TrainingCertificateNumber = Clean(r.TrainingCertificateNumber);
        x.PayerName = Clean(r.PayerName); x.PayerMemberId = Clean(r.PayerMemberId); x.PayerGroupNumber = Clean(r.PayerGroupNumber); x.PayerPlanName = Clean(r.PayerPlanName); x.PayerEffectiveFrom = r.PayerEffectiveFrom; x.PayerEffectiveTo = r.PayerEffectiveTo; x.PayerPhone = Clean(r.PayerPhone);
        x.InsuranceAffiliationRequired = r.InsuranceAffiliationRequired;
        x.PayerAffiliationsJson = JsonSerializer.Serialize((r.PayerAffiliations ?? []).Select(a => new EmployeePayerAffiliation(Clean(a.Company), Clean(a.Payer), Clean(a.PayerId), a.ApplicationFaxedDate, a.AffiliationDate, a.AffiliationEndDate)));
        if (r.PayerAffiliations is { Count: > 0 })
        {
            var primaryAffiliation = r.PayerAffiliations[0];
            x.PayerName = Clean(primaryAffiliation.Payer);
            x.PayerMemberId = Clean(primaryAffiliation.PayerId);
            x.PayerEffectiveFrom = primaryAffiliation.AffiliationDate;
            x.PayerEffectiveTo = primaryAffiliation.AffiliationEndDate;
        }
        var backgroundInformation = r.BackgroundInformation ?? EmptyBackgroundInformation();
        x.EmployeeBackgroundInformationJson = JsonSerializer.Serialize(new EmployeeBackgroundInformation(
            (backgroundInformation.Checks ?? []).Select(check => new EmployeeBackgroundCheck(check.CheckDate, check.ReceivedDate, Clean(check.LicenseType), Clean(check.StudyType), check.Received)).ToArray(),
            backgroundInformation.MantouxGivenDate, backgroundInformation.MantouxCheckedDate, backgroundInformation.MantouxReceived,
            backgroundInformation.MedicalTestingDate, backgroundInformation.MedicalTestingReceived));
        var trainingInformation = r.TrainingInformation ?? EmptyTrainingInformation();
        x.EmployeeTrainingInformationJson = JsonSerializer.Serialize(new EmployeeTrainingInformation(
            (trainingInformation.Courses ?? []).Select(course => new EmployeeCourse(Clean(course.CourseName), course.QualifyingPoints, course.CompletedOn, course.ObtainedPoints, Clean(course.Notes))).ToArray(),
            (trainingInformation.Trainings ?? []).Select(item => new EmployeeTrainingSkill(Clean(item.TrainingName), Clean(item.JobTitle), item.QualifyingPoints, item.CompletedOn, item.ExpiresOn, item.ObtainedPoints, Clean(item.Notes), item.AllowedTraining)).ToArray()));
        if (trainingInformation.Trainings is { Count: > 0 })
        {
            var primaryTraining = trainingInformation.Trainings[0];
            x.TrainingName = Clean(primaryTraining.TrainingName); x.TrainingProvider = Clean(primaryTraining.JobTitle);
            x.TrainingCompletedOn = primaryTraining.CompletedOn; x.TrainingExpiresOn = primaryTraining.ExpiresOn;
        }
        var contacts = (r.Contacts ?? []).Select(contact => new EmployeeContact(Clean(contact.Name), Clean(contact.Phone), Clean(contact.Relationship))).ToArray();
        x.EmployeeContactsJson = JsonSerializer.Serialize(contacts);
        if (contacts.Length > 0)
        {
            x.EmergencyContactName = contacts[0].Name; x.EmergencyContactPhone = contacts[0].Phone;
            x.EmergencyContactRelationship = contacts[0].Relationship;
        }
        else if (r.Contacts is not null)
        {
            x.EmergencyContactName = ""; x.EmergencyContactPhone = ""; x.EmergencyContactRelationship = "";
            x.EmergencyContactEmail = ""; x.EmergencyContactAddress = "";
        }
        x.AllowAggregator = r.AllowAggregator; x.OverrideRestriction = r.OverrideRestriction; x.EvvOnly = r.EvvOnly; x.RestrictDateAndTime = r.RestrictDateAndTime; x.EvvEmail = Clean(r.EvvEmail);
        if (!string.IsNullOrWhiteSpace(r.EvvPassword)) x.EvvPasswordProtected = workforceSecrets.Protect(r.EvvPassword);
        if (!string.IsNullOrWhiteSpace(r.IvrPin)) x.IvrPinProtected = workforceSecrets.Protect(r.IvrPin);
        x.EvvIvrAuthorization = Clean(r.EvvIvrAuthorization); x.EmployeeTier = Clean(r.EmployeeTier); x.TierEffectiveDate = r.TierEffectiveDate; x.IsOnboarding = r.IsOnboarding; x.AllowVisitScreen = r.AllowVisitScreen; x.AllowTravelTime = r.AllowTravelTime; x.AllowManualEntry = r.AllowManualEntry; x.AllowOneTimeLogin = r.AllowOneTimeLogin; x.UpdatedAtUtc = clock.GetUtcNow();
    }

    private async Task ReplaceLocations(Guid id, IReadOnlyList<Guid> ids, CancellationToken ct)
    { db.UserLocations.RemoveRange(await db.UserLocations.Where(x => x.UserId == id).ToArrayAsync(ct)); db.UserLocations.AddRange(ids.Distinct().Select(locationId => new ApplicationUserLocation { UserId = id, LocationId = locationId })); }
    private static string Clean(string? value) => value?.Trim() ?? "";
    private static string Csv(IEnumerable<string>? values) => string.Join(',', values?.Select(Clean).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase) ?? []);
    private static EmployeeBackgroundInformation EmptyBackgroundInformation() => new([], null, null, false, null, false);
    private static EmployeeTrainingInformation EmptyTrainingInformation() => new([], []);
}
