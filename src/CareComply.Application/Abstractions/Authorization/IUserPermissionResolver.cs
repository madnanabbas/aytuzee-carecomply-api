namespace CareComply.Application.Abstractions.Authorization;

public interface IUserPermissionResolver
{
    Task<IReadOnlySet<string>> ResolveAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
