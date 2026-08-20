namespace CareComply.Application.Abstractions.Identity;

public interface IIdentityUserReader
{
    Task<IdentityUserSnapshot?> FindByIdAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
