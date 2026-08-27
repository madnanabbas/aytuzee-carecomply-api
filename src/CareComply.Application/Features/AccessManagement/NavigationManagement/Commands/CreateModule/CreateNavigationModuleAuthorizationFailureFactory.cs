using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.NavigationManagement.Commands.CreateModule;

public sealed class CreateNavigationModuleAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<CreateNavigationModuleResult>
{
    public CreateNavigationModuleResult Create(AuthorizationFailure failure) =>
        CreateNavigationModuleResult.Denied(failure);
}
