using CareComply.Application.Abstractions.Auditing;
using CareComply.Application.Abstractions.Authorization;
using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Authorization;
using CareComply.Infrastructure.Auditing;
using CareComply.Infrastructure.Authorization;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Identity.Development;
using CareComply.Infrastructure.Identity.Staging;
using CareComply.Infrastructure.Persistence;
using CareComply.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CareComply.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    private const string ConnectionStringName = "CareComply";
    private const string DefaultSchema = "dbo";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"The required connection string '{ConnectionStringName}' is not configured.");
        }

        services.AddSingleton<AuditLogImmutabilityInterceptor>();
        services.AddDbContext<CareComplyDbContext>((serviceProvider, options) =>
        {
            options.UseSqlServer(
                    connectionString,
                    sqlServerOptions =>
                    {
                        sqlServerOptions.EnableRetryOnFailure();
                        sqlServerOptions.MigrationsHistoryTable("__EFMigrationsHistory", DefaultSchema);
                    })
                .AddInterceptors(
                    serviceProvider.GetRequiredService<AuditLogImmutabilityInterceptor>());
        });

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 15;
                options.Password.RequiredUniqueChars = 1;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireDigit = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<CareComplyDbContext>();

        services.AddSingleton(TimeProvider.System);
        services.Configure<DevelopmentIdentityOptions>(
            configuration.GetSection(DevelopmentIdentityOptions.SectionName));
        services.AddOptions<StagingIdentityOptions>().Bind(
        configuration.GetSection(StagingIdentityOptions.SectionName));

        services.AddScoped<StagingIdentityInitializer>();
        services.AddOptions<MfaPolicyOptions>()
            .Bind(configuration.GetSection(MfaPolicyOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Issuer),
                "The MFA issuer is required.")
            .Validate(
                options => options.RequiredPermissions.Length > 0
                    && options.RequiredPermissions.All(Permissions.IsDefined),
                "MFA required permissions must be non-empty canonical permissions.")
            .ValidateOnStart();
        services.AddScoped<DevelopmentIdentityInitializer>();
        services.AddSingleton<LoginPasswordTimingProtector>();
        services.AddScoped<IIdentityAuthentication, IdentityAuthenticationService>();
        services.AddScoped<IIdentityMfa, IdentityMfaService>();
        services.AddScoped<IMfaRequirementEvaluator, PermissionBasedMfaRequirementEvaluator>();
        services.AddScoped<MfaFlowSession>();
        services.AddScoped<IIdentitySessionValidator, IdentitySessionValidator>();
        services.AddScoped<IIdentityUserReader, IdentityUserReader>();
        services.AddScoped<IUserPermissionResolver, EfUserPermissionResolver>();
        services.AddScoped<IAuditExecution, EfAuditExecution>();

        return services;
    }
}
