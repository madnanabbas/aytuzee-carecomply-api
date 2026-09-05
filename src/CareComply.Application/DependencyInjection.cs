using FluentValidation;
using CareComply.Application.Authorization;
using CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.CreateFeature;
using CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.CreateModule;
using CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.UpdateFeature;
using CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.UpdateModule;
using CareComply.Application.Features.AccessManagement.NavigationManagement.Queries.ListNavigation;
using CareComply.Application.Features.AccessManagement.Users.Queries.ListUsers;
using CareComply.Application.Features.AccessManagement.Users.Queries.GetUser;
using CareComply.Application.Features.AccessManagement.Users.Commands.UpdateUserGeneral;
using CareComply.Application.Features.AccessManagement.Users.Commands.SetUserStatus;
using CareComply.Application.Features.AccessManagement.Users.Commands.SetUserMfa;
using CareComply.Application.Features.AccessManagement.Users.Commands.ReplaceUserRoles;
using CareComply.Application.Features.AccessManagement.Users.Commands.CreateUser;
using CareComply.Application.Features.AccessManagement.Roles.Queries.ListRoles;
using CareComply.Application.Features.AccessManagement.RoleManagement.Queries.ListRoles;
using CareComply.Application.Features.AccessManagement.RoleManagement.Queries.GetRole;
using CareComply.Application.Features.AccessManagement.RoleManagement.Queries.GetPermissionCatalog;
using CareComply.Application.Features.AccessManagement.RoleManagement.Commands.CreateRole;
using CareComply.Application.Features.AccessManagement.RoleManagement.Commands.UpdateRoleGeneral;
using CareComply.Application.Features.AccessManagement.RoleManagement.Commands.ReplaceRolePermissions;
using CareComply.Application.Features.Clients.Commands.CreateClient;
using CareComply.Application.Features.Clients.Commands.UpdateClient;
using CareComply.Application.Features.Clients.Commands.ChangeClientStatus;
using CareComply.Application.Features.Clients.Queries.ListClients;
using CareComply.Application.Features.Clients.Queries.GetClient;
using CareComply.Application.Features.Clients.Queries.RevealClientSsn;
using CareComply.Application.Features.ClientTodos;
using CareComply.Application.Features.ClientCaregivers;
using CareComply.Application.Features.ClientHospitalizations;
using Microsoft.Extensions.DependencyInjection;

namespace CareComply.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<
            IAuthorizationFailureResultFactory<ListNavigationManagementResult>,
            ListNavigationManagementAuthorizationFailureFactory>();
        services.AddScoped<
            IAuthorizationFailureResultFactory<CreateNavigationModuleResult>,
            CreateNavigationModuleAuthorizationFailureFactory>();
        services.AddScoped<
            IAuthorizationFailureResultFactory<UpdateNavigationModuleResult>,
            UpdateNavigationModuleAuthorizationFailureFactory>();
        services.AddScoped<
            IAuthorizationFailureResultFactory<CreateNavigationFeatureResult>,
            CreateNavigationFeatureAuthorizationFailureFactory>();
        services.AddScoped<
            IAuthorizationFailureResultFactory<UpdateNavigationFeatureResult>,
            UpdateNavigationFeatureAuthorizationFailureFactory>();
        services.AddScoped<
            IAuthorizationFailureResultFactory<ListUsersResult>,
            ListUsersAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<ListRolesResult>, ListRolesAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<ListManagedRolesResult>, ListManagedRolesAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<GetManagedRoleResult>, GetManagedRoleAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<GetRolePermissionCatalogResult>, GetRolePermissionCatalogAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<CreateManagedRoleResult>, CreateManagedRoleAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<UpdateManagedRoleGeneralResult>, UpdateManagedRoleGeneralAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<ReplaceManagedRolePermissionsResult>, ReplaceManagedRolePermissionsAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<CreateUserResult>, CreateUserAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<GetUserResult>, GetUserAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<UpdateUserGeneralResult>, UpdateUserGeneralAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<SetUserStatusResult>, SetUserStatusAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<SetUserMfaResult>, SetUserMfaAuthorizationFailureFactory>();
        services.AddScoped<
            IAuthorizationFailureResultFactory<ReplaceUserRolesResult>,
            ReplaceUserRolesAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<CreateClientResult>, CreateClientAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<UpdateClientResult>, UpdateClientAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<ChangeClientStatusResult>, ChangeClientStatusAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<ListClientsResult>, ListClientsAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<GetClientResult>, GetClientAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<RevealClientSsnResult>, RevealClientSsnAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<ListClientTodosResult>, ListClientTodosAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<ListClientTodoAssigneesResult>, ListClientTodoAssigneesAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<ClientTodoOperationResult>, ClientTodoOperationAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<ClientCaregiverListResult>, ClientCaregiverListAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<ClientCaregiverCandidatesResult>, ClientCaregiverCandidatesAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<ClientCaregiverOperationResult>, ClientCaregiverOperationAuthorizationFailureFactory>();
        services.AddScoped<IAuthorizationFailureResultFactory<HospitalizationResult>, HospitalizationAuthorizationFailureFactory>();
        return services;
    }
}
