namespace CareComply.Application.Abstractions.Identity;

public interface IIdentityAuthentication
{
    Task<IdentityAuthenticationOutcome> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken);
}
