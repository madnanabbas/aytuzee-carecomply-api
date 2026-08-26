using FluentValidation;
using CareComply.Application.Authorization;
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
using Microsoft.Extensions.DependencyInjection;

namespace CareComply.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
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
        return services;
    }
}
