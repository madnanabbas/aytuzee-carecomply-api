using CareComply.Infrastructure.Clients;
using CareComply.Infrastructure.Employees;
using CareComply.Infrastructure.Companies;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Abstractions.Employees;
using CareComply.Application.Abstractions.Companies;
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
using CareComply.Application.Abstractions.Tenancy;
using CareComply.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        services.AddDataProtection()
            .SetApplicationName("CareComply");

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
        services.AddOptions<ClientGuestInvitationOptions>()
            .Bind(configuration.GetSection(ClientGuestInvitationOptions.SectionName))
            .Validate(options => options.IsValid(), "A trusted G-Link intake URL and positive lifetime are required.")
            .ValidateOnStart();

        AddInitializationInfrastructure(services, configuration);
        AddIdentityInfrastructure(services, configuration);
        AddAccessManagementInfrastructure(services);
        AddClientInfrastructure(services);
        AddEmployeeInfrastructure(services);
        services.AddScoped<IAuditExecution, EfAuditExecution>();

        return services;
    }

    private static void AddInitializationInfrastructure(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.Configure<DevelopmentIdentityOptions>(
            configuration.GetSection(DevelopmentIdentityOptions.SectionName));
        services.AddOptions<StagingIdentityOptions>().Bind(
            configuration.GetSection(StagingIdentityOptions.SectionName));
        services.AddScoped<StagingRoleReconciler>();
        services.AddScoped<StagingLocationReconciler>();
        services.AddScoped<StagingUserReconciler>();
        services.AddScoped(serviceProvider => new StagingIdentityInitializer(
            serviceProvider.GetRequiredService<ILogger<StagingIdentityInitializer>>(),
            serviceProvider.GetRequiredService<IOptions<StagingIdentityOptions>>(),
            serviceProvider.GetRequiredService<StagingRoleReconciler>(),
            serviceProvider.GetRequiredService<StagingLocationReconciler>(),
            serviceProvider.GetRequiredService<StagingUserReconciler>()));
        services.AddScoped<DevelopmentRoleReconciler>();
        services.AddScoped<DevelopmentUserReconciler>();
        services.AddScoped(serviceProvider => new DevelopmentIdentityInitializer(
            serviceProvider.GetRequiredService<ILogger<DevelopmentIdentityInitializer>>(),
            serviceProvider.GetRequiredService<IOptions<DevelopmentIdentityOptions>>(),
            serviceProvider.GetRequiredService<DevelopmentRoleReconciler>(),
            serviceProvider.GetRequiredService<DevelopmentUserReconciler>()));
        services.AddScoped<DevelopmentWorkforceInitializer>();
    }

    private static void AddIdentityInfrastructure(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<MfaPolicyOptions>()
            .Bind(configuration.GetSection(MfaPolicyOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "The MFA issuer is required.")
            .Validate(
                options => options.RequiredPermissions.All(Permissions.IsDefined),
                "MFA permission configuration contains an unknown permission.")
            .ValidateOnStart();
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
    }

    private static void AddAccessManagementInfrastructure(IServiceCollection services)
    {
        services.AddScoped<IRoleDirectory, EfRoleDirectory>();
        services.AddScoped<RoleManagementReader>();
        services.AddScoped<RoleCompanyAccessValidator>();
        services.AddScoped<RolePermissionSynchronizer>();
        services.AddScoped<CareComply.Application.Abstractions.AccessManagement.IRoleManagement, EfRoleManagement>();
        services.AddScoped<IUserDirectory, EfUserDirectory>();
        services.AddScoped<IUserProvisioning, IdentityUserProvisioning>();
        services.AddScoped<UserCompanyLocationValidator>();
        services.AddScoped<UserLocationSynchronizer>();
        services.AddScoped<IUserGeneralAdministration, IdentityUserGeneralAdministration>();
        services.AddScoped<IUserStatusAdministration, IdentityUserStatusAdministration>();
        services.AddScoped<IUserMfaAdministration, IdentityUserMfaAdministration>();
        services.AddScoped<IUserRoleAdministration, IdentityUserRoleAdministration>();
        services.AddScoped<IUserPermissionResolver, EfUserPermissionResolver>();
        services.AddScoped<ITenantScopeAccessor, EfTenantScopeAccessor>();
        services.AddScoped<ICompanyAdministration, EfCompanyAdministration>();
        services.AddScoped<ILocationAdministration, EfLocationAdministration>();
        services.AddScoped<TenantUserAccess>();
        services.AddScoped<INavigationDirectory, EfNavigationDirectory>();
        services.AddScoped<NavigationManagementReader>();
        services.AddScoped<NavigationHierarchyValidator>();
        services.AddScoped<NavigationMutationWriter>();
        services.AddScoped<INavigationAdministration, EfNavigationAdministration>();
    }

    private static void AddClientInfrastructure(IServiceCollection services)
    {
        services.AddScoped<IClientDirectory, EfClientDirectory>();
        services.AddScoped<IClientSupervisorCandidateReader, EfClientSupervisorCandidateReader>();
        services.AddScoped<ClientAssignmentResolver>();
        services.AddScoped<ClientBasicDetailsPersistence>();
        services.AddScoped<ClientContactInformationPersistence>();
        services.AddScoped<ClientCaregiverEligibility>();
        services.AddScoped<ClientChangeDetector>();
        services.AddScoped<ClientServiceSynchronizer>();
        services.AddScoped<ClientLanguageSynchronizer>();
        services.AddScoped<IClientAdministration, EfClientAdministration>();
        services.AddScoped<IClientTodoStore, EfClientTodoStore>();
        services.AddScoped<IClientCaregiverStore, EfClientCaregiverStore>();
        services.AddScoped<IClientResponsiblePartyStore, EfClientResponsiblePartyStore>();
        services.AddScoped<IClientContactStore, EfClientContactStore>();
        services.AddScoped<IClientCareCoordinatorStore, EfClientCareCoordinatorStore>();
        services.AddScoped<IClientClinicalContactStore, EfClientClinicalContactStore>();
        services.AddScoped<IClientHospitalizationStore, EfClientHospitalizationStore>();
        services.AddScoped<IClientSensitiveData, DataProtectionClientSensitiveData>();
        services.AddScoped<IClientGuestInvitationCreator, EfClientGuestInvitationCreator>();
        services.AddScoped<IClientGuestInvitationEmailSender, AzureCommunicationClientGuestInvitationEmailSender>();
        services.AddScoped<IClientGuestInvitationSender, EfClientGuestInvitationSender>();
        services.AddScoped<IClientGuestInvitationPublicResolver, EfClientGuestInvitationPublicResolver>();
        services.AddScoped<IClientGuestSessionService, EfClientGuestSessionService>();
    }

    private static void AddEmployeeInfrastructure(IServiceCollection services)
    {
        services.AddScoped<EfEmployeeDetailsReader>();
        services.AddScoped<IEmployeeDirectory, EfEmployeeDirectory>();
        services.AddScoped<EmployeePersistenceMapper>();
        services.AddScoped<EmployeePersistenceValidator>();
        services.AddScoped<EmployeeLocationSynchronizer>();
        services.AddScoped<IEmployeeAdministration, EfEmployeeAdministration>();
        services.AddScoped<IEmployeeSensitiveData, DataProtectionEmployeeSensitiveData>();
    }
}
