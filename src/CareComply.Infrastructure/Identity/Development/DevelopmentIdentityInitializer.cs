using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CareComply.Infrastructure.Identity.Development;

public sealed class DevelopmentIdentityInitializer
{
    private static readonly Action<ILogger, int, int, Exception?> LogFixturesInitialized =
        LoggerMessage.Define<int, int>(
            LogLevel.Information,
            new EventId(1, "DevelopmentIdentityFixturesInitialized"),
            "Development identity fixtures initialized with {RoleCount} roles and {UserCount} users.");

    private readonly ILogger<DevelopmentIdentityInitializer> logger;
    private readonly IOptions<DevelopmentIdentityOptions> options;
    private readonly DevelopmentRoleReconciler roleReconciler;
    private readonly DevelopmentUserReconciler userReconciler;

    internal DevelopmentIdentityInitializer(
        ILogger<DevelopmentIdentityInitializer> logger,
        IOptions<DevelopmentIdentityOptions> options,
        DevelopmentRoleReconciler roleReconciler,
        DevelopmentUserReconciler userReconciler)
    {
        this.logger = logger;
        this.options = options;
        this.roleReconciler = roleReconciler;
        this.userReconciler = userReconciler;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var configuration = options.Value;
        if (!configuration.Enabled)
        {
            throw new InvalidOperationException(
                "Development identity fixture initialization is not enabled.");
        }

        var passwords = DevelopmentIdentitySecrets.ReadRequiredPasswords(configuration.Users);
        var roles = await roleReconciler.ReconcileAsync(cancellationToken);
        await userReconciler.ReconcileAsync(passwords, roles, cancellationToken);

        LogFixturesInitialized(
            logger,
            DevelopmentIdentityFixtures.Roles.Count,
            DevelopmentIdentityFixtures.Users.Count,
            null);
    }
}
