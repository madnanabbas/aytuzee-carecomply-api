using CareComply.Infrastructure.Clients;
using CareComply.Infrastructure.Employees;
using CareComply.Infrastructure.Forms;
using CareComply.Infrastructure.ClientDocuments;
using CareComply.Infrastructure.Companies;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Abstractions.Employees;
using CareComply.Application.Abstractions.Forms;
using CareComply.Application.Abstractions.ClientDocuments;
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
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;

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
        AddFormContentInfrastructure(services, configuration);
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
        services.Configure<DevelopmentFormTemplateOptions>(
            configuration.GetSection(DevelopmentFormTemplateOptions.SectionName));
        services.AddScoped<DevelopmentFormTemplateInitializer>();
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
        services.AddScoped<IPayerStore, EfPayerStore>();
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
        services.AddScoped<IClientCoverageStore, EfClientCoverageStore>();
        services.AddScoped<IClientServiceAgreementStore, EfClientServiceAgreementStore>();
        services.AddScoped<IClientDocumentAdministration, EfClientDocumentAdministration>();
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

    private static void AddFormContentInfrastructure(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(FormContentStorageOptions.SectionName);
        var configured = section.Get<FormContentStorageOptions>() ?? new FormContentStorageOptions();
        ValidateFormContentStorageOptions(configured);
        services.AddOptions<FormContentStorageOptions>().Bind(section);

        if (string.Equals(
            configured.Provider,
            FormContentStorageProviders.AzureBlob,
            StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
            services.AddSingleton(serviceProvider =>
            {
                var blobOptions = new BlobClientOptions
                {
                    Retry =
                    {
                        Mode = RetryMode.Exponential,
                        MaxRetries = configured.AzureBlob.MaximumRetries,
                        Delay = configured.AzureBlob.RetryDelay,
                        MaxDelay = configured.AzureBlob.MaximumRetryDelay,
                        NetworkTimeout = configured.AzureBlob.NetworkTimeout,
                    },
                };
                return new BlobServiceClient(
                    new Uri(configured.AzureBlob.AccountEndpoint, UriKind.Absolute),
                    serviceProvider.GetRequiredService<TokenCredential>(),
                    blobOptions);
            });
            services.AddSingleton(serviceProvider =>
                serviceProvider.GetRequiredService<BlobServiceClient>()
                    .GetBlobContainerClient(configured.AzureBlob.ContainerName));
            services.AddSingleton(serviceProvider => new ClientDocumentBlobContainer(
                serviceProvider.GetRequiredService<BlobServiceClient>()
                    .GetBlobContainerClient(configured.AzureBlob.ClientDocumentContainerName)));
            services.AddSingleton<IFormContentStore, AzureBlobFormContentStore>();
            services.AddSingleton<IClientDocumentContentStore, AzureBlobClientDocumentContentStore>();
        }
        else
        {
            services.AddSingleton<IFormContentStore, LocalFormContentStore>();
            services.AddSingleton<IClientDocumentContentStore, LocalClientDocumentContentStore>();
        }

        services.AddScoped<IClientFormDirectory, EfClientFormDirectory>();
        services.AddScoped<IFormTemplateAdministration, EfFormTemplateAdministration>();
        services.AddScoped<IFormTemplatePdfValidator, PdfPigFormTemplatePdfValidator>();
        services.AddScoped<IClientDocumentFileValidator, ClientDocumentFileValidator>();
    }

    private static void ValidateFormContentStorageOptions(FormContentStorageOptions options)
    {
        if (options.MaximumUploadSizeBytes <= 0 || options.ClientDocumentMaximumUploadSizeBytes <= 0
            || options.ClientDocumentMaximumUploadSizeBytes > 10 * 1024 * 1024)
            throw new InvalidOperationException("The form-content maximum upload size must be positive.");

        if (string.Equals(
            options.Provider,
            FormContentStorageProviders.Local,
            StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(options.LocalRootPath)
                || string.IsNullOrWhiteSpace(options.ClientDocumentLocalRootPath))
                throw new InvalidOperationException("The local form-content storage configuration is incomplete.");
            return;
        }

        if (!string.Equals(
            options.Provider,
            FormContentStorageProviders.AzureBlob,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The form-content storage provider is not supported.");
        }

        if (!Uri.TryCreate(options.AzureBlob.AccountEndpoint, UriKind.Absolute, out var endpoint)
            || !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(endpoint.Query)
            || !string.IsNullOrEmpty(endpoint.Fragment)
            || !string.IsNullOrEmpty(endpoint.UserInfo)
            || endpoint.AbsolutePath is not ("" or "/")
            || !IsValidContainerName(options.AzureBlob.ContainerName)
            || !IsValidContainerName(options.AzureBlob.ClientDocumentContainerName))
        {
            throw new InvalidOperationException("The Azure Blob form-content storage configuration is incomplete or invalid.");
        }

        if (options.AzureBlob.MaximumRetries is < 0 or > 5
            || options.AzureBlob.RetryDelay <= TimeSpan.Zero
            || options.AzureBlob.RetryDelay > TimeSpan.FromSeconds(30)
            || options.AzureBlob.MaximumRetryDelay <= TimeSpan.Zero
            || options.AzureBlob.MaximumRetryDelay < options.AzureBlob.RetryDelay
            || options.AzureBlob.MaximumRetryDelay > TimeSpan.FromMinutes(2)
            || options.AzureBlob.NetworkTimeout <= TimeSpan.Zero
            || options.AzureBlob.NetworkTimeout > TimeSpan.FromMinutes(2))
        {
            throw new InvalidOperationException("The Azure Blob form-content reliability configuration is invalid.");
        }
    }

    private static bool IsValidContainerName(string value)
    {
        if (value.Length is < 3 or > 63
            || !char.IsAsciiLetterOrDigit(value[0])
            || !char.IsAsciiLetterOrDigit(value[^1]))
        {
            return false;
        }

        var previousWasHyphen = false;
        foreach (var character in value)
        {
            var valid = char.IsAsciiDigit(character)
                || character is >= 'a' and <= 'z'
                || character == '-';
            if (!valid || (character == '-' && previousWasHyphen)) return false;
            previousWasHyphen = character == '-';
        }
        return true;
    }
}
