using CareComply.Application.Abstractions.Navigation;
using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.CreateFeature;

public sealed record CreateNavigationFeatureResult(
    NavigationAdministrationMutation? Mutation,
    AuthorizationFailure? AuthorizationFailure)
{
    public string ReasonCode =>
        AuthorizationFailure?.ToString()
        ?? Mutation?.Status.ToString()
        ?? "Unavailable";

    public static CreateNavigationFeatureResult Succeeded(
        NavigationAdministrationMutation mutation) => new(mutation, null);

    public static CreateNavigationFeatureResult Denied(
        AuthorizationFailure failure) => new(null, failure);
}
