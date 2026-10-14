namespace CareComply.Application.Abstractions.Clients;

public interface IClientCoverageStore
{
    Task<IReadOnlyList<ClientCoverageItem>?> ListAsync(Guid clientId, CancellationToken cancellationToken);
    Task<ClientCoverageMutation> CreateAsync(SaveClientCoverage request, CancellationToken cancellationToken);
    Task<ClientCoverageMutation> UpdateAsync(UpdateClientCoverage request, CancellationToken cancellationToken);
}

public sealed record ClientCoverageItem(
    Guid Id, Guid? PayerId, string PayerName, string? ManualPayerName, string? MemberId,
    string? MedicaidId, DateOnly? EffectiveFrom, DateOnly? EffectiveTo, bool IsVerified,
    bool IsPrimary, string EditorVersion);

public sealed record SaveClientCoverage(
    Guid ClientId, Guid? PayerId, string? ManualPayerName, string? MemberId, string? MedicaidId,
    DateOnly? EffectiveFrom, DateOnly? EffectiveTo, bool IsVerified, bool IsPrimary,
    DateTimeOffset NowUtc);

public sealed record UpdateClientCoverage(
    Guid ClientId, Guid CoverageId, Guid? PayerId, string? ManualPayerName, string? MemberId,
    string? MedicaidId, DateOnly? EffectiveFrom, DateOnly? EffectiveTo, bool IsVerified,
    bool IsPrimary, string EditorVersion, DateTimeOffset NowUtc);

public enum ClientCoverageMutationStatus
{
    Succeeded, ClientNotFound, NotFound, InvalidPayer, DuplicatePrimary, Conflict,
}

public sealed record ClientCoverageMutation(
    ClientCoverageMutationStatus Status, ClientCoverageItem? Item = null);
