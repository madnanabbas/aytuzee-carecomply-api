using CareComply.Api.Authorization;
using CareComply.Api.Contracts.Employees;
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
    UserManager<ApplicationUser> users, IDataProtectionProvider protection, TimeProvider clock) : ControllerBase
{
    private readonly IDataProtector ssn = protection.CreateProtector("CareComply.Employee.SSN.v1");
    private readonly IDataProtector workforceSecrets = protection.CreateProtector("CareComply.Employee.EVV.v1");

    [HttpGet, HasPermission(Permissions.Employee.View)]
    public async Task<ActionResult<IReadOnlyList<EmployeeListItemResponse>>> List([FromQuery] string? search, CancellationToken ct)
    {
        var query = await Scoped(ct); if (query is null) return Forbid();
        if (!string.IsNullOrWhiteSpace(search)) { var value = search.Trim(); query = query.Where(x => x.FirstName.Contains(value) || x.LastName.Contains(value) || x.Email!.Contains(value)); }
        var items = await query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).Select(x => new EmployeeListItemResponse(
            x.Id, x.FirstName, x.LastName, x.PreferredName, x.Email!, x.PhoneNumber ?? "", x.JobTitle, x.IsActive,
            x.HireDate, db.UserLocations.Where(link => link.UserId == x.Id).Join(db.CompanyLocations, link => link.LocationId,
                location => location.Id, (_, location) => location.Name).OrderBy(name => name).ToArray())).ToArrayAsync(ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}"), HasPermission(Permissions.Employee.View)]
    public async Task<ActionResult<EmployeeDetailsResponse>> Get(Guid id, CancellationToken ct)
    { var query = await Scoped(ct); var employee = query is null ? null : await query.SingleOrDefaultAsync(x => x.Id == id, ct); return employee is null ? NotFound() : Ok(await Map(employee, ct)); }

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
        return CreatedAtAction(nameof(Get), new { id = employee.Id }, await Map(employee, ct));
    }

    [HttpPut("{id:guid}"), HasPermission(Permissions.Employee.Edit)]
    public async Task<ActionResult<EmployeeDetailsResponse>> Update(Guid id, SaveEmployeeRequest request, CancellationToken ct)
    {
        var query = await Scoped(ct); var employee = query is null ? null : await query.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (employee is null) return NotFound();
        if (!await Valid(request, employee.CompanyId!.Value, ct, id)) return ValidationProblem(ModelState);
        Apply(employee, request); employee.NormalizedEmail = users.NormalizeEmail(employee.Email); employee.NormalizedUserName = users.NormalizeName(employee.Email); employee.UserName = employee.Email;
        await ReplaceLocations(id, request.LocationIds!, ct); await db.SaveChangesAsync(ct); return Ok(await Map(employee, ct));
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
        if (r.SupervisorUserId is { } supervisorId && !await db.Users.AnyAsync(x => x.Id == supervisorId && x.CompanyId == companyId && x.IsEmployee && x.IsActive, ct)) ModelState.AddModelError(nameof(r.SupervisorUserId), "Select an active employee from this company as supervisor.");
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
        return ModelState.IsValid;
    }

    private void Apply(ApplicationUser x, SaveEmployeeRequest r)
    {
        x.FirstName = r.FirstName.Trim(); x.MiddleName = Clean(r.MiddleName); x.LastName = r.LastName.Trim(); x.PreferredName = Clean(r.PreferredName);
        x.Email = r.Email.Trim(); x.PhoneNumber = Clean(r.Phone); x.SecondaryPhone = Clean(r.SecondaryPhone); x.JobTitle = r.JobTitle.Trim(); x.SecondaryJobTitle = Clean(r.SecondaryJobTitle);
        x.IsActive = r.IsActive; x.HireDate = r.HireDate; x.StartOfServiceDate = r.StartOfServiceDate; x.InactiveDate = r.InactiveDate; x.DateOfBirth = r.DateOfBirth; x.Gender = Clean(r.Gender);
        if (!string.IsNullOrWhiteSpace(r.Ssn)) x.SsnProtected = ssn.Protect(r.Ssn.Replace("-", "", StringComparison.Ordinal));
        x.LanguagesCsv = string.Join(',', r.Languages?.Select(Clean).Where(v => v.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase) ?? []); x.HoursPerMonth = r.HoursPerMonth; x.SupervisorUserId = r.SupervisorUserId;
        x.Umpi = Clean(r.Umpi); x.Npi = Clean(r.Npi); x.LicenseId = Clean(r.LicenseId); x.DriverLicenseExpiresOn = r.DriverLicenseExpiresOn;
        x.StreetAddress = Clean(r.StreetAddress); x.Apartment = Clean(r.Apartment); x.City = Clean(r.City); x.State = Clean(r.State); x.PostalCode = Clean(r.PostalCode); x.County = Clean(r.County); x.EmployeeNotes = Clean(r.Notes);
        x.CareProgram = Clean(r.CareProgram); x.HasUsedDifferentName = r.HasUsedDifferentName; x.PreviousName = r.HasUsedDifferentName ? Clean(r.PreviousName) : ""; x.HeightInches = r.HeightInches; x.WeightPounds = r.WeightPounds; x.HairColor = Clean(r.HairColor); x.EyeColor = Clean(r.EyeColor); x.BirthPlace = Clean(r.BirthPlace); x.CfssCertificationNumber = Clean(r.CfssCertificationNumber);
        x.HasWorkingHourRestrictions = r.HasWorkingHourRestrictions; x.AvailableDaysCsv = Csv(r.AvailableDays); x.AvailableStartTime = r.HasWorkingHourRestrictions ? r.AvailableStartTime : null; x.AvailableEndTime = r.HasWorkingHourRestrictions ? r.AvailableEndTime : null; x.PreviousAddressesJson = JsonSerializer.Serialize((r.PreviousAddresses ?? []).Select(a => new EmployeePreviousAddress(Clean(a.Street), Clean(a.PostalCode), Clean(a.City), Clean(a.County), Clean(a.State))).Where(a => a.Street.Length > 0 || a.City.Length > 0));
        x.MaritalStatus = Clean(r.MaritalStatus); x.Ethnicity = Clean(r.Ethnicity); x.QualificationsCsv = Csv(r.Qualifications); x.CanWorkWithSmokers = r.CanWorkWithSmokers; x.SmokerRestrictionExplanation = r.CanWorkWithSmokers ? "" : Clean(r.SmokerRestrictionExplanation); x.IsUnionMember = r.IsUnionMember; x.IsMilitaryVeteran = r.IsMilitaryVeteran; x.MilitaryServiceExplanation = r.IsMilitaryVeteran ? Clean(r.MilitaryServiceExplanation) : ""; x.Race = Clean(r.Race); x.DemographicDescription = Clean(r.DemographicDescription); x.CanWorkWithPets = r.CanWorkWithPets; x.PetRestrictionExplanation = r.CanWorkWithPets ? "" : Clean(r.PetRestrictionExplanation); x.ClientTypesCsv = Csv(r.ClientTypes); x.HasOtherJob = r.HasOtherJob; x.OtherEmployerName = r.HasOtherJob ? Clean(r.OtherEmployerName) : ""; x.OtherEmploymentDuration = r.HasOtherJob ? Clean(r.OtherEmploymentDuration) : ""; x.CanDriveForWork = r.CanDriveForWork; x.PreviousWorkExperience = Clean(r.PreviousWorkExperience);
        x.EmergencyContactName = Clean(r.EmergencyContactName); x.EmergencyContactRelationship = Clean(r.EmergencyContactRelationship); x.EmergencyContactPhone = Clean(r.EmergencyContactPhone); x.EmergencyContactEmail = Clean(r.EmergencyContactEmail); x.EmergencyContactAddress = Clean(r.EmergencyContactAddress);
        x.TrainingName = Clean(r.TrainingName); x.TrainingProvider = Clean(r.TrainingProvider); x.TrainingCompletedOn = r.TrainingCompletedOn; x.TrainingExpiresOn = r.TrainingExpiresOn; x.TrainingCertificateNumber = Clean(r.TrainingCertificateNumber);
        x.PayerName = Clean(r.PayerName); x.PayerMemberId = Clean(r.PayerMemberId); x.PayerGroupNumber = Clean(r.PayerGroupNumber); x.PayerPlanName = Clean(r.PayerPlanName); x.PayerEffectiveFrom = r.PayerEffectiveFrom; x.PayerEffectiveTo = r.PayerEffectiveTo; x.PayerPhone = Clean(r.PayerPhone);
        x.AllowAggregator = r.AllowAggregator; x.OverrideRestriction = r.OverrideRestriction; x.EvvOnly = r.EvvOnly; x.RestrictDateAndTime = r.RestrictDateAndTime; x.EvvEmail = Clean(r.EvvEmail);
        if (!string.IsNullOrWhiteSpace(r.EvvPassword)) x.EvvPasswordProtected = workforceSecrets.Protect(r.EvvPassword);
        if (!string.IsNullOrWhiteSpace(r.IvrPin)) x.IvrPinProtected = workforceSecrets.Protect(r.IvrPin);
        x.EvvIvrAuthorization = Clean(r.EvvIvrAuthorization); x.EmployeeTier = Clean(r.EmployeeTier); x.TierEffectiveDate = r.TierEffectiveDate; x.IsOnboarding = r.IsOnboarding; x.AllowVisitScreen = r.AllowVisitScreen; x.AllowTravelTime = r.AllowTravelTime; x.AllowManualEntry = r.AllowManualEntry; x.AllowOneTimeLogin = r.AllowOneTimeLogin; x.UpdatedAtUtc = clock.GetUtcNow();
    }

    private async Task ReplaceLocations(Guid id, IReadOnlyList<Guid> ids, CancellationToken ct)
    { db.UserLocations.RemoveRange(await db.UserLocations.Where(x => x.UserId == id).ToArrayAsync(ct)); db.UserLocations.AddRange(ids.Distinct().Select(locationId => new ApplicationUserLocation { UserId = id, LocationId = locationId })); }
    private async Task<EmployeeDetailsResponse> Map(ApplicationUser x, CancellationToken ct)
    { var locations = await db.UserLocations.Where(link => link.UserId == x.Id).Select(link => link.LocationId).ToArrayAsync(ct); return new(x.Id, x.CompanyId!.Value, locations, x.FirstName, x.MiddleName, x.LastName, x.PreferredName, x.Email!, x.PhoneNumber ?? "", x.SecondaryPhone, x.JobTitle, x.SecondaryJobTitle, x.IsActive, x.HireDate, x.StartOfServiceDate, x.InactiveDate, x.DateOfBirth, x.Gender, x.SsnProtected.Length > 0, x.LanguagesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries), x.HoursPerMonth, x.SupervisorUserId, x.Umpi, x.Npi, x.LicenseId, x.DriverLicenseExpiresOn, x.StreetAddress, x.Apartment, x.City, x.State, x.PostalCode, x.County, x.EmployeeNotes, x.CareProgram, x.HasUsedDifferentName, x.PreviousName, x.HeightInches, x.WeightPounds, x.HairColor, x.EyeColor, x.BirthPlace, x.CfssCertificationNumber, x.HasWorkingHourRestrictions, Split(x.AvailableDaysCsv), x.AvailableStartTime, x.AvailableEndTime, Addresses(x.PreviousAddressesJson), x.MaritalStatus, x.Ethnicity, Split(x.QualificationsCsv), x.CanWorkWithSmokers, x.SmokerRestrictionExplanation, x.IsUnionMember, x.IsMilitaryVeteran, x.MilitaryServiceExplanation, x.Race, x.DemographicDescription, x.CanWorkWithPets, x.PetRestrictionExplanation, Split(x.ClientTypesCsv), x.HasOtherJob, x.OtherEmployerName, x.OtherEmploymentDuration, x.CanDriveForWork, x.PreviousWorkExperience, x.EmergencyContactName, x.EmergencyContactRelationship, x.EmergencyContactPhone, x.EmergencyContactEmail, x.EmergencyContactAddress, x.TrainingName, x.TrainingProvider, x.TrainingCompletedOn, x.TrainingExpiresOn, x.TrainingCertificateNumber, x.PayerName, x.PayerMemberId, x.PayerGroupNumber, x.PayerPlanName, x.PayerEffectiveFrom, x.PayerEffectiveTo, x.PayerPhone, x.AllowAggregator, x.OverrideRestriction, x.EvvOnly, x.RestrictDateAndTime, x.EvvEmail, x.EvvPasswordProtected.Length > 0, x.IvrPinProtected.Length > 0, x.EvvIvrAuthorization, x.EmployeeTier, x.TierEffectiveDate, x.IsOnboarding, x.AllowVisitScreen, x.AllowTravelTime, x.AllowManualEntry, x.AllowOneTimeLogin); }
    private static string Clean(string? value) => value?.Trim() ?? "";
    private static string Csv(IEnumerable<string>? values) => string.Join(',', values?.Select(Clean).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase) ?? []);
    private static string[] Split(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static EmployeePreviousAddress[] Addresses(string json) { try { return JsonSerializer.Deserialize<EmployeePreviousAddress[]>(json) ?? []; } catch (JsonException) { return []; } }
}
