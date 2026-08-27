using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.UpdateFeature;

public sealed class UpdateNavigationFeatureAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<UpdateNavigationFeatureResult>
{
    public UpdateNavigationFeatureResult Create(AuthorizationFailure failure) =>
        UpdateNavigationFeatureResult.Denied(failure);
}
