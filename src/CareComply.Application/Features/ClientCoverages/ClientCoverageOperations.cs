using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Domain.Clients;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Features.ClientCoverages;

public sealed record ListClientCoveragesQuery(Guid ClientId)
    : IQuery<ClientCoverageResult>, IRequirePermission
{ public string RequiredPermission => Permissions.ClientCoverage.View; }

public sealed record CreateClientCoverageCommand(
    Guid ClientId, Guid? PayerId, string? ManualPayerName, string? MemberId, string? MedicaidId,
    DateOnly? EffectiveFrom, DateOnly? EffectiveTo, bool? IsVerified, bool? IsPrimary)
    : ICommand<ClientCoverageResult>, IRequirePermission, IAuditableRequest<ClientCoverageResult>
{
    public string RequiredPermission => Permissions.ClientCoverage.Create;
    public AuditEvent CreateAuditEvent(ClientCoverageResult response) =>
        ClientCoverageAudit.Create("ClientCoverage.Created", ClientId, response, null);
}

public sealed record UpdateClientCoverageCommand(
    Guid ClientId, Guid CoverageId, Guid? PayerId, string? ManualPayerName, string? MemberId,
    string? MedicaidId, DateOnly? EffectiveFrom, DateOnly? EffectiveTo, bool? IsVerified,
    bool? IsPrimary, string? EditorVersion)
    : ICommand<ClientCoverageResult>, IRequirePermission, IAuditableRequest<ClientCoverageResult>
{
    public string RequiredPermission => Permissions.ClientCoverage.Edit;
    public AuditEvent CreateAuditEvent(ClientCoverageResult response) =>
        ClientCoverageAudit.Create("ClientCoverage.Updated", ClientId, response, CoverageId);
}

public sealed record ClientCoverageResult(
    IReadOnlyList<ClientCoverageItem>? Items, ClientCoverageItem? Item,
    ClientCoverageMutationStatus? Status, AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<ClientCoverageResult>
{
    public static ClientCoverageResult Listed(IReadOnlyList<ClientCoverageItem>? items) => new(items, null, null, null);
    public static ClientCoverageResult Changed(ClientCoverageMutation mutation) => new(null, mutation.Item, mutation.Status, null);
    public static ClientCoverageResult Denied(AuthorizationFailure failure) => new(null, null, null, failure);
}

public sealed class ListClientCoveragesHandler(IClientCoverageStore store)
    : IQueryHandler<ListClientCoveragesQuery, ClientCoverageResult>
{
    public async ValueTask<ClientCoverageResult> Handle(ListClientCoveragesQuery query, CancellationToken cancellationToken) =>
        ClientCoverageResult.Listed(await store.ListAsync(query.ClientId, cancellationToken));
}

public sealed class CreateClientCoverageHandler(IClientCoverageStore store, TimeProvider timeProvider)
    : ICommandHandler<CreateClientCoverageCommand, ClientCoverageResult>
{
    public async ValueTask<ClientCoverageResult> Handle(CreateClientCoverageCommand command, CancellationToken cancellationToken) =>
        ClientCoverageResult.Changed(await store.CreateAsync(new(command.ClientId, command.PayerId,
            command.ManualPayerName, command.MemberId, command.MedicaidId, command.EffectiveFrom,
            command.EffectiveTo, command.IsVerified!.Value, command.IsPrimary!.Value,
            timeProvider.GetUtcNow()), cancellationToken));
}

public sealed class UpdateClientCoverageHandler(IClientCoverageStore store, TimeProvider timeProvider)
    : ICommandHandler<UpdateClientCoverageCommand, ClientCoverageResult>
{
    public async ValueTask<ClientCoverageResult> Handle(UpdateClientCoverageCommand command, CancellationToken cancellationToken) =>
        ClientCoverageResult.Changed(await store.UpdateAsync(new(command.ClientId, command.CoverageId,
            command.PayerId, command.ManualPayerName, command.MemberId, command.MedicaidId,
            command.EffectiveFrom, command.EffectiveTo, command.IsVerified!.Value,
            command.IsPrimary!.Value, command.EditorVersion!, timeProvider.GetUtcNow()), cancellationToken));
}

public abstract class ClientCoverageValidator<T> : AbstractValidator<T> where T : class
{
    protected void AddRules(Func<T, Guid?> payerId, Func<T, string?> manualPayerName,
        Func<T, string?> memberId, Func<T, string?> medicaidId, Func<T, DateOnly?> effectiveFrom,
        Func<T, DateOnly?> effectiveTo, Func<T, bool?> isVerified, Func<T, bool?> isPrimary)
    {
        RuleFor(x => x).Must(x => payerId(x).HasValue ^ !string.IsNullOrWhiteSpace(manualPayerName(x)))
            .WithName("Payer").WithMessage("Select a payer or enter a manual payer name, but not both.");
        RuleFor(x => payerId(x)).NotEqual(Guid.Empty).When(x => payerId(x).HasValue).OverridePropertyName("PayerId");
        RuleFor(x => manualPayerName(x)).MaximumLength(ClientCoverage.MaximumPayerNameLength).OverridePropertyName("ManualPayerName");
        RuleFor(x => memberId(x)).MaximumLength(ClientCoverage.MaximumMemberIdLength).OverridePropertyName("MemberId");
        RuleFor(x => medicaidId(x)).MaximumLength(ClientCoverage.MaximumMedicaidIdLength).OverridePropertyName("MedicaidId");
        RuleFor(x => effectiveTo(x)).GreaterThanOrEqualTo(x => effectiveFrom(x)!.Value)
            .When(x => effectiveFrom(x).HasValue && effectiveTo(x).HasValue).OverridePropertyName("EffectiveTo");
        RuleFor(x => isVerified(x)).NotNull().OverridePropertyName("IsVerified");
        RuleFor(x => isPrimary(x)).NotNull().OverridePropertyName("IsPrimary");
    }
}

public sealed class CreateClientCoverageValidator : ClientCoverageValidator<CreateClientCoverageCommand>
{
    public CreateClientCoverageValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        AddRules(x => x.PayerId, x => x.ManualPayerName, x => x.MemberId, x => x.MedicaidId,
            x => x.EffectiveFrom, x => x.EffectiveTo, x => x.IsVerified, x => x.IsPrimary);
    }
}

public sealed class UpdateClientCoverageValidator : ClientCoverageValidator<UpdateClientCoverageCommand>
{
    public UpdateClientCoverageValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.CoverageId).NotEmpty();
        RuleFor(x => x.EditorVersion).NotEmpty();
        AddRules(x => x.PayerId, x => x.ManualPayerName, x => x.MemberId, x => x.MedicaidId,
            x => x.EffectiveFrom, x => x.EffectiveTo, x => x.IsVerified, x => x.IsPrimary);
    }
}

internal static class ClientCoverageAudit
{
    public static AuditEvent Create(string action, Guid clientId, ClientCoverageResult response, Guid? requestedId)
    {
        var succeeded = response.Status == ClientCoverageMutationStatus.Succeeded && response.Item is not null;
        return new(action, "ClientCoverage", (response.Item?.Id ?? requestedId)?.ToString("N") ?? "not-created",
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded ? AuditMetadata.Create(("clientId", clientId.ToString("N")),
                    ("isPrimary", response.Item!.IsPrimary.ToString()),
                    ("isVerified", response.Item.IsVerified.ToString()))
                : AuditMetadata.Create(("reasonCode", response.Status?.ToString() ?? "denied")));
    }
}
