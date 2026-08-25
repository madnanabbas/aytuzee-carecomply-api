namespace CareComply.Application.Abstractions.Identity;

public interface IMfaRequirementEvaluator
{
    Task<bool> IsRequiredAsync(Guid userId, CancellationToken cancellationToken);

    Task<bool> IsRequiredByPermissionsAsync(Guid userId, CancellationToken cancellationToken);
}
