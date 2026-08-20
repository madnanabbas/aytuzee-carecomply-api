namespace CareComply.Application.Abstractions.Identity;

public interface IIdentitySessionValidator
{
    Task<bool> IsValidAsync(
        Guid userId,
        string securityStamp,
        bool hasMfaAssurance,
        CancellationToken cancellationToken);
}
