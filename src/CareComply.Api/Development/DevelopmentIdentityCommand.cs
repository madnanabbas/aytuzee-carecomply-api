using CareComply.Infrastructure.Identity.Development;

namespace CareComply.Api.Development;

public static class DevelopmentIdentityCommand
{
    public const string Argument = "--initialize-development-identity";

    public static async Task<bool> TryRunAsync(
        IServiceProvider services,
        IHostEnvironment environment,
        IConfiguration configuration,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default)
    {
        if (!arguments.Contains(Argument, StringComparer.Ordinal))
        {
            return false;
        }

        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Development identity fixtures can run only in the Development environment.");
        }

        if (!configuration.GetValue<bool>($"{DevelopmentIdentityOptions.SectionName}:Enabled"))
        {
            throw new InvalidOperationException(
                "Development identity fixture initialization requires the explicit enabled flag.");
        }

        await using var scope = services.CreateAsyncScope();
        var initializer = scope.ServiceProvider
            .GetRequiredService<DevelopmentIdentityInitializer>();
        await initializer.InitializeAsync(cancellationToken);
        return true;
    }
}
