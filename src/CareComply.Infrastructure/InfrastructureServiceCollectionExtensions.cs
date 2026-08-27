using CareComply.Application.Abstractions.Auditing;
using CareComply.Application.Abstractions.Authorization;
using CareComply.Application.Abstractions.Identity;
using CareComply.Application.Abstractions.Navigation;
using CareComply.Application.Authorization;
using CareComply.Infrastructure.Auditing;
using CareComply.Infrastructure.AccessManagement;
using CareComply.Infrastructure.Authorization;
using CareComply.Infrastructure.Identity;
using CareComply.Infrastructure.Identity.Development;
using CareComply.Infrastructure.Identity.Staging;
using CareComply.Infrastructure.Navigation;
using CareComply.Infrastructure.Persistence;
using CareComply.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CareComply.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    private const string ConnectionStringName = "CareComply";
    private const string DefaultSchema = "dbo";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDataProtection();

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
                options.Password.RequiredLength = UserPasswordPolicy.MinimumLength;
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
                options.Tokens.PasswordResetTokenProvider =
                    CareComplyPasswordResetTokenProvider.ProviderName;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<CareComplyDbContext>()
            .AddTokenProvider<CareComplyPasswordResetTokenProvider>(
                CareComplyPasswordResetTokenProvider.ProviderName);

        services.Configure<CareComplyPasswordResetTokenProviderOptions>(options =>
        {
            options.Name = CareComplyPasswordResetTokenProvider.ProviderName;
            options.TokenLifespan = TimeSpan.FromMinutes(60);
        });
        services.AddOptions<PasswordRecoveryOptions>()
            .Bind(configuration.GetSection(PasswordRecoveryOptions.SectionName));
        services.AddOptions<EmailDeliveryOptions>()
            .Bind(configuration.GetSection(EmailDeliveryOptions.SectionName));

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
        services.AddScoped<IPasswordRecovery, IdentityPasswordRecovery>();
        services.AddScoped<AzureCommunicationEmailDelivery>();
        services.AddScoped<IPasswordRecoveryEmailSender, AzureCommunicationPasswordRecoveryEmailSender>();
        services.AddScoped<IMfaEmailSender, AzureCommunicationMfaEmailSender>();
        services.AddScoped<IPasswordResetLinkFactory, ConfiguredPasswordResetLinkFactory>();
        services.AddScoped<IIdentityMfa, IdentityMfaService>();
        services.AddScoped<IMfaRequirementEvaluator, PermissionBasedMfaRequirementEvaluator>();
        services.AddScoped<MfaFlowSession>();
        services.AddScoped<IIdentitySessionValidator, IdentitySessionValidator>();
        services.AddScoped<IIdentityUserReader, IdentityUserReader>();
        services.AddScoped<IRoleDirectory, EfRoleDirectory>();
        services.AddScoped<CareComply.Application.Abstractions.AccessManagement.IRoleManagement, EfRoleManagement>();
        services.AddScoped<IUserDirectory, EfUserDirectory>();
        services.AddScoped<IUserProvisioning, IdentityUserProvisioning>();
        services.AddScoped<IUserGeneralAdministration, IdentityUserGeneralAdministration>();
        services.AddScoped<IUserStatusAdministration, IdentityUserStatusAdministration>();
        services.AddScoped<IUserMfaAdministration, IdentityUserMfaAdministration>();
        services.AddScoped<IUserRoleAdministration, IdentityUserRoleAdministration>();
        services.AddScoped<IUserPermissionResolver, EfUserPermissionResolver>();
        services.AddScoped<INavigationDirectory, EfNavigationDirectory>();
        services.AddScoped<INavigationAdministration, EfNavigationAdministration>();
        services.AddScoped<IAuditExecution, EfAuditExecution>();

        return services;
    }
}
