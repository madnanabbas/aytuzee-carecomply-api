using CareComply.Application.Abstractions.Navigation;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.UpdateModule;

public sealed record UpdateNavigationModuleResult(
    NavigationAdministrationMutation? Mutation,
    AuthorizationFailure? AuthorizationFailure)
{
    public string ReasonCode =>
        AuthorizationFailure?.ToString()
        ?? Mutation?.Status.ToString()
        ?? "Unavailable";

    public static UpdateNavigationModuleResult Succeeded(
        NavigationAdministrationMutation mutation) => new(mutation, null);

    public static UpdateNavigationModuleResult Denied(
        AuthorizationFailure failure) => new(null, failure);
}
