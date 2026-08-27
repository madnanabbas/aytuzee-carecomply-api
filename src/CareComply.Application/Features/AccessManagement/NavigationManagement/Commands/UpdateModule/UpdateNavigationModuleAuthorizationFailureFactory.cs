using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.UpdateModule;

public sealed class UpdateNavigationModuleAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<UpdateNavigationModuleResult>
{
    public UpdateNavigationModuleResult Create(AuthorizationFailure failure) =>
        UpdateNavigationModuleResult.Denied(failure);
}
