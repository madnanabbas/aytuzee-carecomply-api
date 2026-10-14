namespace CareComply.Application.Abstractions.Companies;

public interface IPayerStore
{
    Task<IReadOnlyList<PayerItem>?> ListAsync(bool includeInactive, CancellationToken cancellationToken);
    Task<PayerMutation> CreateAsync(string name, DateTimeOffset nowUtc, CancellationToken cancellationToken);
    Task<PayerMutation> UpdateAsync(Guid payerId, string name, bool isActive, string editorVersion,
        DateTimeOffset nowUtc, CancellationToken cancellationToken);
}

public sealed record PayerItem(Guid Id, string Name, bool IsActive, string EditorVersion);

public enum PayerMutationStatus { Succeeded, TenantUnavailable, NotFound, DuplicateName, Conflict }

public sealed record PayerMutation(PayerMutationStatus Status, PayerItem? Item = null);
