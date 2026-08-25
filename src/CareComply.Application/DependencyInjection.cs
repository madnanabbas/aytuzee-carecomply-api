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
