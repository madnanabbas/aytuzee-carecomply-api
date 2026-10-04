using CareComply.Application.Abstractions.Employees;
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using CareComply.Infrastructure.Identity;

namespace CareComply.Infrastructure.Employees;

internal sealed class EfEmployeeDetailsReader(
    CareComplyDbContext db,
    ITenantScopeAccessor tenants)
{
    public async Task<EmployeeDetailsResponse?> GetAsync(Guid employeeId, CancellationToken ct)
    {
        var tenant = await tenants.GetAsync(ct);
        if (tenant is null)
        {
            return null;
        }

        var query = db.Users.AsNoTracking().Where(user => user.IsEmployee && user.Id == employeeId);
        if (!tenant.IsPlatformAdministrator)
        {
            query = query.Where(user => user.CompanyId == tenant.CompanyId);
        }

        var employee = await query.SingleOrDefaultAsync(ct);
        return employee is null ? null : await MapAsync(employee, ct);
    }

    private async Task<EmployeeDetailsResponse> MapAsync(ApplicationUser employee, CancellationToken cancellationToken)
    {
        var locations = await db.UserLocations
            .Where(link => link.UserId == employee.Id)
            .Select(link => link.LocationId)
            .ToArrayAsync(cancellationToken);

        return new EmployeeDetailsResponse(
            employee.Id, employee.CompanyId!.Value, locations,
            employee.FirstName, employee.MiddleName, employee.LastName,
            employee.PreferredName, employee.Email!, employee.PhoneNumber ?? "",
            employee.SecondaryPhone, employee.JobTitle, employee.SecondaryJobTitle,
            employee.IsActive, employee.HireDate, employee.StartOfServiceDate,
            employee.InactiveDate, employee.DateOfBirth, employee.Gender,
            employee.SsnProtected.Length > 0, employee.LanguagesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries), employee.HoursPerMonth,
            employee.Umpi, employee.Npi,
            employee.LicenseId, employee.DriverLicenseExpiresOn, employee.StreetAddress,
            employee.Apartment, employee.City, employee.State,
            employee.PostalCode, employee.County, employee.EmployeeNotes,
            employee.CareProgram, employee.HasUsedDifferentName, employee.PreviousName,
            employee.HeightInches, employee.WeightPounds, employee.HairColor,
            employee.EyeColor, employee.BirthPlace, employee.CfssCertificationNumber,
            employee.HasWorkingHourRestrictions, Split(employee.AvailableDaysCsv), employee.AvailableStartTime,
            employee.AvailableEndTime, Addresses(employee.PreviousAddressesJson), employee.MaritalStatus,
            employee.Ethnicity, Split(employee.QualificationsCsv), employee.CanWorkWithSmokers,
            employee.SmokerRestrictionExplanation, employee.IsUnionMember, employee.IsMilitaryVeteran,
            employee.MilitaryServiceExplanation, employee.Race, employee.DemographicDescription,
            employee.CanWorkWithPets, employee.PetRestrictionExplanation, Split(employee.ClientTypesCsv),
            employee.HasOtherJob, employee.OtherEmployerName, employee.OtherEmploymentDuration,
            employee.CanDriveForWork, employee.PreviousWorkExperience, employee.EmergencyContactName,
            employee.EmergencyContactRelationship, employee.EmergencyContactPhone, employee.EmergencyContactEmail,
            employee.EmergencyContactAddress, employee.TrainingName, employee.TrainingProvider,
            employee.TrainingCompletedOn, employee.TrainingExpiresOn, employee.TrainingCertificateNumber,
            employee.PayerName, employee.PayerMemberId, employee.PayerGroupNumber,
            employee.PayerPlanName, employee.PayerEffectiveFrom, employee.PayerEffectiveTo,
            employee.PayerPhone, employee.InsuranceAffiliationRequired, PayerAffiliations(employee.PayerAffiliationsJson),
            BackgroundInformation(employee.EmployeeBackgroundInformationJson),
            TrainingInformation(employee.EmployeeTrainingInformationJson, employee),
            Contacts(employee.EmployeeContactsJson, employee),
            employee.AllowAggregator, employee.OverrideRestriction, employee.EvvOnly,
            employee.RestrictDateAndTime, employee.EvvEmail, employee.EvvPasswordProtected.Length > 0,
            employee.IvrPinProtected.Length > 0, employee.EvvIvrAuthorization, employee.EmployeeTier,
            employee.TierEffectiveDate, employee.IsOnboarding, employee.AllowVisitScreen,
            employee.AllowTravelTime, employee.AllowManualEntry, employee.AllowOneTimeLogin,
            IdentityEditorVersion.Create(employee.ConcurrencyStamp ?? string.Empty));
    }
    private static string[] Split(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static EmployeePreviousAddress[] Addresses(string json)
    {
        try { return JsonSerializer.Deserialize<EmployeePreviousAddress[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static EmployeePayerAffiliation[] PayerAffiliations(string json)
    {
        try { return JsonSerializer.Deserialize<EmployeePayerAffiliation[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static EmployeeBackgroundInformation BackgroundInformation(string json)
    {
        try { return JsonSerializer.Deserialize<EmployeeBackgroundInformation>(json) ?? EmptyBackgroundInformation(); }
        catch (JsonException) { return EmptyBackgroundInformation(); }
    }

    private static EmployeeBackgroundInformation EmptyBackgroundInformation() => new([], null, null, false, null, false);

    private static EmployeeTrainingInformation TrainingInformation(string json, ApplicationUser user)
    {
        try
        {
            var value = JsonSerializer.Deserialize<EmployeeTrainingInformation>(json);
            if (value?.Courses is not null && value.Trainings is not null) return value;
        }
        catch (JsonException) { }

        return string.IsNullOrWhiteSpace(user.TrainingName)
            ? EmptyTrainingInformation()
            : new([], [new(user.TrainingName, user.TrainingProvider, null,
                user.TrainingCompletedOn, user.TrainingExpiresOn, null, "", false)]);
    }

    private static EmployeeTrainingInformation EmptyTrainingInformation() => new([], []);

    private static EmployeeContact[] Contacts(string json, ApplicationUser user)
    {
        try
        {
            var contacts = JsonSerializer.Deserialize<EmployeeContact[]>(json);
            if (contacts is { Length: > 0 }) return contacts;
        }
        catch (JsonException) { }

        return string.IsNullOrWhiteSpace(user.EmergencyContactName)
            ? []
            : [new(user.EmergencyContactName, user.EmergencyContactPhone, user.EmergencyContactRelationship)];
    }
}
