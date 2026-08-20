namespace CareComply.Application.Authorization;

public interface IAuthorizationFailureResultFactory<out TResponse>
{
    TResponse Create(AuthorizationFailure failure);
}
