using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.CreateFeature;

public sealed class CreateNavigationFeatureAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<CreateNavigationFeatureResult>
{
    public CreateNavigationFeatureResult Create(AuthorizationFailure failure) =>
        CreateNavigationFeatureResult.Denied(failure);
}
