namespace CareComply.Application.Abstractions.Identity;

public interface IMfaRequirementEvaluator
{
    Task<bool> IsRequiredAsync(Guid userId, CancellationToken cancellationToken);
}
