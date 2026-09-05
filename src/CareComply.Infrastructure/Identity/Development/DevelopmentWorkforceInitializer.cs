using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace CareComply.Infrastructure.Identity.Development;

/// <summary>
/// Supplies non-login workforce records until the Employee module owns employee provisioning.
/// Development-only invocation is enforced by the API composition root.
/// </summary>
public sealed class DevelopmentWorkforceInitializer(
    ILogger<DevelopmentWorkforceInitializer> logger,
    TimeProvider timeProvider,
    UserManager<ApplicationUser> userManager)
{
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

            var result = isNew
                ? await userManager.CreateAsync(user)
                : await userManager.UpdateAsync(user);
            EnsureSucceeded(result, fixture.Email);
        }

        LogInitialized(logger, Fixtures.Count, null);
    }

    private static void EnsureSucceeded(IdentityResult result, string email)
    {
        if (result.Succeeded) return;
        var codes = string.Join(", ", result.Errors.Select(x => x.Code).Order(StringComparer.Ordinal));
        throw new InvalidOperationException($"Development employee '{email}' could not be initialized. Error codes: {codes}.");
    }

    private static IReadOnlyList<EmployeeFixture> Fixtures { get; } =
    [
        new("maya.thompson@carecomply.local", "Maya", "Thompson", "Personal Care Assistant", "(612) 555-0211"),
        new("daniel.kim@carecomply.local", "Daniel", "Kim", "Home Health Aide", "(612) 555-0212"),
        new("olivia.martinez@carecomply.local", "Olivia", "Martinez", "Direct Support Professional", "(612) 555-0213"),
    ];

    private sealed record EmployeeFixture(string Email, string FirstName, string LastName, string JobTitle, string Phone);
}
