using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CareComply.Infrastructure.Persistence;
using CareComply.Infrastructure.Tenancy;

namespace CareComply.Infrastructure.Identity.Development;

/// <summary>
/// Supplies non-login workforce records until the Employee module owns employee provisioning.
/// Development-only invocation is enforced by the API composition root.
/// </summary>
public sealed class DevelopmentWorkforceInitializer(
    ILogger<DevelopmentWorkforceInitializer> logger,
    TimeProvider timeProvider,
    UserManager<ApplicationUser> userManager,
    CareComplyDbContext context,
    IDataProtectionProvider protection)
{
    private readonly IDataProtector ssnProtector = protection.CreateProtector("CareComply.Employee.SSN.v1");
    private readonly IDataProtector workforceProtector = protection.CreateProtector("CareComply.Employee.EVV.v1");
    private static readonly Action<ILogger, int, Exception?> LogInitialized =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(1, "DevelopmentWorkforceInitialized"),
            "Development workforce initialized with {EmployeeCount} employees.");

    public async Task InitializeAsync()
    {
        foreach (var fixture in Fixtures)
        {
            var user = await userManager.FindByEmailAsync(fixture.Email);
            var isNew = user is null;
            var now = timeProvider.GetUtcNow();

            if (user is null)
            {
                user = new ApplicationUser
                {
                    CompanyId = TenantDefaults.DefaultCompanyId,
                    Email = fixture.Email,
                    UserName = fixture.Email,
                    EmailConfirmed = true,
                    CreatedAtUtc = now,
                };
            }

            user.FirstName = fixture.FirstName;
            user.LastName = fixture.LastName;
            user.PhoneNumber = fixture.Phone;
            user.JobTitle = fixture.JobTitle;
            user.IsEmployee = true;
            user.IsActive = true;
            user.UpdatedAtUtc = now;

            if (fixture.Email == "maya.thompson@carecomply.local")
            {
                ApplyCompleteDemoProfile(user);
            }

            var result = isNew
                ? await userManager.CreateAsync(user)
                : await userManager.UpdateAsync(user);
            EnsureSucceeded(result, fixture.Email);

            var locationIds = await context.CompanyLocations
                .Where(x => x.CompanyId == TenantDefaults.DefaultCompanyId && x.IsActive)
                .OrderByDescending(x => x.IsMain)
                .ThenBy(x => x.Name)
                .Select(x => x.Id)
                .Take(fixture.Email == "maya.thompson@carecomply.local" ? 3 : 1)
                .ToArrayAsync();
            var existing = await context.UserLocations.Where(x => x.UserId == user.Id).Select(x => x.LocationId).ToArrayAsync();
            context.UserLocations.AddRange(locationIds.Except(existing).Select(locationId =>
                new ApplicationUserLocation { UserId = user.Id, LocationId = locationId }));
        }

        await context.SaveChangesAsync();

        LogInitialized(logger, Fixtures.Count, null);
    }

    private static void EnsureSucceeded(IdentityResult result, string email)
    {
        if (result.Succeeded) return;
        var codes = string.Join(", ", result.Errors.Select(x => x.Code).Order(StringComparer.Ordinal));
        throw new InvalidOperationException($"Development employee '{email}' could not be initialized. Error codes: {codes}.");
    }

    private void ApplyCompleteDemoProfile(ApplicationUser user)
    {
        user.MiddleName = "Renee";
        user.PreferredName = "Maya";
        user.SecondaryJobTitle = "Medication Assistant";
        user.HireDate = new DateOnly(2022, 6, 13);
        user.StartOfServiceDate = new DateOnly(2022, 6, 20);
        user.DateOfBirth = new DateOnly(1990, 4, 18);
        user.Gender = "Female";
        if (string.IsNullOrEmpty(user.SsnProtected)) user.SsnProtected = ssnProtector.Protect("999-88-1234");
        user.SecondaryPhone = "(612) 555-0214";
        user.LanguagesCsv = "English,Spanish";
        user.HoursPerMonth = 160;
        user.Umpi = "A123456789";
        user.Npi = "1234567893";
        user.LicenseId = "MN-PCA-20481";
        user.DriverLicenseExpiresOn = new DateOnly(2028, 4, 18);
        user.StreetAddress = "1425 Portland Avenue";
        user.Apartment = "Apt 204";
        user.City = "Minneapolis";
        user.State = "MN";
        user.PostalCode = "55404";
        user.County = "Hennepin";
        user.EmployeeNotes = "Experienced PCA assigned to community and in-home care. Bilingual in English and Spanish.";
        user.CareProgram = "PCA Traditional";
        user.HeightInches = 65;
        user.WeightPounds = 142;
        user.HairColor = "Black";
        user.EyeColor = "Brown";
        user.BirthPlace = "Minneapolis, Minnesota, United States";
        user.CfssCertificationNumber = "CFSS-MN-260481";
        user.HasWorkingHourRestrictions = true;
        user.AvailableDaysCsv = "Monday,Tuesday,Wednesday,Thursday,Friday";
        user.AvailableStartTime = new TimeOnly(7, 0);
        user.AvailableEndTime = new TimeOnly(17, 0);
        user.PreviousAddressesJson = "[{\"Street\":\"820 University Avenue\",\"PostalCode\":\"55104\",\"City\":\"St. Paul\",\"County\":\"Ramsey\",\"State\":\"MN\"}]";
        user.MaritalStatus = "Married";
        user.Ethnicity = "Not Hispanic or Latino";
        user.QualificationsCsv = "PCA,CFSS Support Worker,Medication Administration";
        user.CanWorkWithSmokers = true;
        user.IsUnionMember = false;
        user.IsMilitaryVeteran = false;
        user.Race = "Black or African American";
        user.CanWorkWithPets = true;
        user.ClientTypesCsv = "Adults,Seniors,Physical disabilities";
        user.HasOtherJob = false;
        user.CanDriveForWork = true;
        user.PreviousWorkExperience = "Four years of in-home personal care and two years supporting adults with physical disabilities.";
        user.EmergencyContactName = "Jordan Thompson";
        user.EmergencyContactRelationship = "Spouse";
        user.EmergencyContactPhone = "(612) 555-0299";
        user.EmergencyContactEmail = "jordan.thompson@example.com";
        user.EmergencyContactAddress = "1425 Portland Avenue, Apt 204, Minneapolis, MN 55404";
        user.TrainingName = "CPR, First Aid and Medication Administration";
        user.TrainingProvider = "American Red Cross";
        user.TrainingCompletedOn = new DateOnly(2026, 2, 12);
        user.TrainingExpiresOn = new DateOnly(2028, 2, 12);
        user.TrainingCertificateNumber = "ARC-MN-2602148";
        user.PayerName = "HealthPartners";
        user.PayerMemberId = "HP-MAYA-48219";
        user.PayerGroupNumber = "GRP-715204";
        user.PayerPlanName = "Open Access Choice";
        user.PayerEffectiveFrom = new DateOnly(2026, 1, 1);
        user.PayerEffectiveTo = new DateOnly(2026, 12, 31);
        user.PayerPhone = "(800) 883-2177";
        user.AllowAggregator = true;
        user.EvvEmail = "maya.thompson@carecomply.local";
        if (string.IsNullOrEmpty(user.EvvPasswordProtected)) user.EvvPasswordProtected = workforceProtector.Protect("DemoEvv!2026");
        if (string.IsNullOrEmpty(user.IvrPinProtected)) user.IvrPinProtected = workforceProtector.Protect("4826");
        user.EvvIvrAuthorization = "EVV and IVR";
        user.EmployeeTier = "Tier 1";
        user.TierEffectiveDate = new DateOnly(2026, 1, 1);
        user.AllowVisitScreen = true;
        user.AllowTravelTime = true;
        user.AllowManualEntry = true;
    }

    private static IReadOnlyList<EmployeeFixture> Fixtures { get; } =
    [
        new("maya.thompson@carecomply.local", "Maya", "Thompson", "Personal Care Assistant", "(612) 555-0211"),
        new("daniel.kim@carecomply.local", "Daniel", "Kim", "Home Health Aide", "(612) 555-0212"),
        new("olivia.martinez@carecomply.local", "Olivia", "Martinez", "Direct Support Professional", "(612) 555-0213"),
    ];

    private sealed record EmployeeFixture(string Email, string FirstName, string LastName, string JobTitle, string Phone);
}
