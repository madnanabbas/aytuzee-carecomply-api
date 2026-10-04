using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CareComply.Infrastructure.Identity.Staging;

public sealed class StagingIdentityInitializer
{
    private readonly ILogger<StagingIdentityInitializer> logger;
    private readonly IOptions<StagingIdentityOptions> options;
    private readonly StagingRoleReconciler roleReconciler;
    private readonly StagingLocationReconciler locationReconciler;
    private readonly StagingUserReconciler userReconciler;

    private static readonly Action<ILogger, int, int, Exception?> LogFixturesInitialized =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(1, "StagingIdentityFixturesInitialized"),
            "Staging identity fixtures initialized with {RoleCount} roles and {UserCount} users.");

    internal StagingIdentityInitializer(
        ILogger<StagingIdentityInitializer> logger,
        IOptions<StagingIdentityOptions> options,
        StagingRoleReconciler roleReconciler,
        StagingLocationReconciler locationReconciler,
        StagingUserReconciler userReconciler)
    {
        this.logger = logger;
        this.options = options;
        this.roleReconciler = roleReconciler;
        this.locationReconciler = locationReconciler;
        this.userReconciler = userReconciler;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var configuration = options.Value;
        if (!configuration.Enabled)
        {
            throw new InvalidOperationException(
                "Staging identity fixture initialization is not enabled.");
        }

        var passwords = StagingIdentitySecrets.ReadRequiredPasswords(configuration.Users);
        var roles = await roleReconciler.ReconcileAsync(cancellationToken);
        await locationReconciler.ReconcileAsync(cancellationToken);
        await userReconciler.ReconcileAsync(
            passwords,
            roles,
            cancellationToken);

        LogFixturesInitialized(
            logger,
            StagingIdentityFixtures.Roles.Count,
            StagingIdentityFixtures.Users.Count,
            null);
    }
}
