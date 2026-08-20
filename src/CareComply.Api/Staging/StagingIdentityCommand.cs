using CareComply.Infrastructure.Identity.Staging;

namespace CareComply.Api.Staging;

public static class StagingIdentityCommand
{
    public const string Argument =
        "--initialize-staging-identity";

    public static async Task<bool> TryRunAsync(
        IServiceProvider services,
        IHostEnvironment environment,
        IConfiguration configuration,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default)
    {
        if (
            !arguments.Contains(
                Argument,
                StringComparer.Ordinal))
        {
            return false;
        }

        if (!environment.IsStaging())
        {
            throw new InvalidOperationException(
                "Staging identity fixtures can run only in the Staging environment.");
        }

        if (
            !configuration.GetValue<bool>(
                $"{StagingIdentityOptions.SectionName}:Enabled"))
        {
            throw new InvalidOperationException(
                "Staging identity fixture initialization requires the explicit enabled flag.");
        }

        await using var scope =
            services.CreateAsyncScope();

        var initializer =
            scope.ServiceProvider
                .GetRequiredService<StagingIdentityInitializer>();

        await initializer.InitializeAsync(
            cancellationToken);

        return true;
    }
}