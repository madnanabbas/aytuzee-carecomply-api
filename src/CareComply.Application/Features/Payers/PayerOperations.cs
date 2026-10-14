using CareComply.Application.Abstractions.Companies;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Domain.Companies;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Features.Payers;

public sealed record ListPayersQuery(bool IncludeInactive) : IQuery<PayerResult>, IRequirePermission
{ public string RequiredPermission => Permissions.Payer.View; }

public sealed record CreatePayerCommand(string? Name) : ICommand<PayerResult>, IRequirePermission,
    IAuditableRequest<PayerResult>
{
    public string RequiredPermission => Permissions.Payer.Create;
    public AuditEvent CreateAuditEvent(PayerResult response) => PayerAudit.Create("Payer.Created", response, null);
}

public sealed record UpdatePayerCommand(Guid PayerId, string? Name, bool? IsActive, string? EditorVersion)
    : ICommand<PayerResult>, IRequirePermission, IAuditableRequest<PayerResult>
{
    public string RequiredPermission => Permissions.Payer.Edit;
    public AuditEvent CreateAuditEvent(PayerResult response) => PayerAudit.Create("Payer.Updated", response, PayerId);
}

public sealed record PayerResult(IReadOnlyList<PayerItem>? Items, PayerItem? Item,
    PayerMutationStatus? Status, AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<PayerResult>
{
    public static PayerResult Listed(IReadOnlyList<PayerItem>? items) => new(items, null, null, null);
    public static PayerResult Changed(PayerMutation mutation) => new(null, mutation.Item, mutation.Status, null);
    public static PayerResult Denied(AuthorizationFailure failure) => new(null, null, null, failure);
}

public sealed class ListPayersHandler(IPayerStore store) : IQueryHandler<ListPayersQuery, PayerResult>
{
    public async ValueTask<PayerResult> Handle(ListPayersQuery query, CancellationToken cancellationToken) =>
        PayerResult.Listed(await store.ListAsync(query.IncludeInactive, cancellationToken));
}

public sealed class CreatePayerHandler(IPayerStore store, TimeProvider timeProvider)
    : ICommandHandler<CreatePayerCommand, PayerResult>
{
    public async ValueTask<PayerResult> Handle(CreatePayerCommand command, CancellationToken cancellationToken) =>
        PayerResult.Changed(await store.CreateAsync(command.Name!, timeProvider.GetUtcNow(), cancellationToken));
}

public sealed class UpdatePayerHandler(IPayerStore store, TimeProvider timeProvider)
    : ICommandHandler<UpdatePayerCommand, PayerResult>
{
    public async ValueTask<PayerResult> Handle(UpdatePayerCommand command, CancellationToken cancellationToken) =>
        PayerResult.Changed(await store.UpdateAsync(command.PayerId, command.Name!, command.IsActive!.Value,
            command.EditorVersion!, timeProvider.GetUtcNow(), cancellationToken));
}

public sealed class CreatePayerValidator : AbstractValidator<CreatePayerCommand>
{
    public CreatePayerValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(Payer.MaximumNameLength);
}

public sealed class UpdatePayerValidator : AbstractValidator<UpdatePayerCommand>
{
    public UpdatePayerValidator()
    {
        RuleFor(x => x.PayerId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Payer.MaximumNameLength);
        RuleFor(x => x.IsActive).NotNull();
        RuleFor(x => x.EditorVersion).NotEmpty();
    }
}

internal static class PayerAudit
{
    public static AuditEvent Create(string action, PayerResult response, Guid? requestedId)
    {
        var succeeded = response.Status == PayerMutationStatus.Succeeded && response.Item is not null;
        return new(action, "Payer", (response.Item?.Id ?? requestedId)?.ToString("N") ?? "not-created",
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded ? AuditMetadata.Create(("isActive", response.Item!.IsActive.ToString()))
                : AuditMetadata.Create(("reasonCode", response.Status?.ToString() ?? "denied")));
    }
}
