using CareComply.Infrastructure.Forms;

namespace CareComply.Api.Development;

public static class DevelopmentFormTemplateCommand
{
    public const string Argument = "--initialize-development-form-templates";

    public static async Task<bool> TryRunAsync(
        IServiceProvider services,
        IHostEnvironment environment,
        IConfiguration configuration,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default)
    {
        if (!arguments.Contains(Argument, StringComparer.Ordinal)) return false;
        if (!environment.IsDevelopment())
            throw new InvalidOperationException("Development form fixtures can run only in Development.");
        if (!configuration.GetValue<bool>($"{DevelopmentFormTemplateOptions.SectionName}:Enabled"))
            throw new InvalidOperationException("Development form fixture initialization requires the explicit enabled flag.");

        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DevelopmentFormTemplateInitializer>()
            .InitializeAsync(cancellationToken);
        return true;
    }
}
