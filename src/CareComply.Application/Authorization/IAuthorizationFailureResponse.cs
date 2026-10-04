namespace CareComply.Application.Authorization;

public interface IAuthorizationFailureResponse<TSelf>
    where TSelf : IAuthorizationFailureResponse<TSelf>
{
    static abstract TSelf Denied(AuthorizationFailure failure);
}
