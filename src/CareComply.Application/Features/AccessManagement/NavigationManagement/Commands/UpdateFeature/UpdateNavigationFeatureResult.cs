using CareComply.Application.Abstractions.Navigation;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.UpdateFeature;

public sealed record UpdateNavigationFeatureResult(
    NavigationAdministrationMutation? Mutation,
    AuthorizationFailure? AuthorizationFailure)
{
    public string ReasonCode =>
        AuthorizationFailure?.ToString()
        ?? Mutation?.Status.ToString()
        ?? "Unavailable";

    public static UpdateNavigationFeatureResult Succeeded(
        NavigationAdministrationMutation mutation) => new(mutation, null);

    public static UpdateNavigationFeatureResult Denied(
        AuthorizationFailure failure) => new(null, failure);
}
