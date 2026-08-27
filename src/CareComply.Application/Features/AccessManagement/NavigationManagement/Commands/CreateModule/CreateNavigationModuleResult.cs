using CareComply.Application.Abstractions.Navigation;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.CreateModule;

public sealed record CreateNavigationModuleResult(
    NavigationAdministrationMutation? Mutation,
    AuthorizationFailure? AuthorizationFailure)
{
    public string ReasonCode =>
        AuthorizationFailure?.ToString()
        ?? Mutation?.Status.ToString()
        ?? "Unavailable";

    public static CreateNavigationModuleResult Succeeded(
        NavigationAdministrationMutation mutation) => new(mutation, null);

    public static CreateNavigationModuleResult Denied(
        AuthorizationFailure failure) => new(null, failure);
}
