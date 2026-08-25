using CareComply.Application.Authorization;

namespace CareComply.Application.Features.AccessManagement.Users.Commands.UpdateUserGeneral;

public sealed class UpdateUserGeneralAuthorizationFailureFactory
    : IAuthorizationFailureResultFactory<UpdateUserGeneralResult>
{
    public UpdateUserGeneralResult Create(AuthorizationFailure failure) =>
        UpdateUserGeneralResult.Denied(failure);
}
