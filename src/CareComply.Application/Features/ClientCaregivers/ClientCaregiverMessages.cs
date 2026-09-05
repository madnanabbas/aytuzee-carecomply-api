using CareComply.Application.Abstractions.Authentication;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Domain.Clients;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Features.ClientCaregivers;

public sealed record ListClientCaregiversQuery(Guid ClientId) : IQuery<ClientCaregiverListResult>, IRequirePermission { public string RequiredPermission => Permissions.ClientCaregiver.View; }
public sealed record ListClientCaregiverCandidatesQuery : IQuery<ClientCaregiverCandidatesResult>, IRequirePermission { public string RequiredPermission => Permissions.ClientCaregiver.View; }
public sealed record CreateClientCaregiverCommand(Guid ClientId, Guid UserId, ClientCaregiverType Type, bool IsPreferred,
    string? Services, DateOnly StartDate, DateOnly? EndDate, string? Notes) : ICommand<ClientCaregiverOperationResult>, IRequirePermission, IAuditableRequest<ClientCaregiverOperationResult>
{ public string RequiredPermission => Permissions.ClientCaregiver.Create; public AuditEvent CreateAuditEvent(ClientCaregiverOperationResult response) => ClientCaregiverAudit.Create("ClientCaregiver.Assigned", ClientId, response); }
public sealed record UpdateClientCaregiverCommand(Guid ClientId, Guid AssignmentId, ClientCaregiverType Type, bool IsPreferred,
    string? Services, DateOnly StartDate, DateOnly? EndDate, string? Notes, string EditorVersion) : ICommand<ClientCaregiverOperationResult>, IRequirePermission, IAuditableRequest<ClientCaregiverOperationResult>
{ public string RequiredPermission => Permissions.ClientCaregiver.Edit; public AuditEvent CreateAuditEvent(ClientCaregiverOperationResult response) => ClientCaregiverAudit.Create("ClientCaregiver.Updated", ClientId, response, AssignmentId); }
public sealed record EndClientCaregiverCommand(Guid ClientId, Guid AssignmentId, DateOnly EndDate, string EditorVersion) : ICommand<ClientCaregiverOperationResult>, IRequirePermission, IAuditableRequest<ClientCaregiverOperationResult>
{ public string RequiredPermission => Permissions.ClientCaregiver.Edit; public AuditEvent CreateAuditEvent(ClientCaregiverOperationResult response) => ClientCaregiverAudit.Create("ClientCaregiver.Ended", ClientId, response, AssignmentId); }

public sealed record ClientCaregiverListResult(ClientCaregiverList? Value, AuthorizationFailure? AuthorizationFailure)
{ public static ClientCaregiverListResult Completed(ClientCaregiverList? value) => new(value, null); public static ClientCaregiverListResult Denied(AuthorizationFailure value) => new(null, value); }
public sealed record ClientCaregiverCandidatesResult(IReadOnlyList<ClientCaregiverCandidate>? Value, AuthorizationFailure? AuthorizationFailure)
{ public static ClientCaregiverCandidatesResult Completed(IReadOnlyList<ClientCaregiverCandidate> value) => new(value, null); public static ClientCaregiverCandidatesResult Denied(AuthorizationFailure value) => new(null, value); }
public sealed record ClientCaregiverOperationResult(ClientCaregiverMutation? Mutation, AuthorizationFailure? AuthorizationFailure)
{ public string ReasonCode => AuthorizationFailure?.ToString() ?? Mutation?.Status.ToString() ?? "Unavailable"; public static ClientCaregiverOperationResult Completed(ClientCaregiverMutation value) => new(value, null); public static ClientCaregiverOperationResult Denied(AuthorizationFailure value) => new(null, value); }
public sealed class ClientCaregiverListAuthorizationFailureFactory : IAuthorizationFailureResultFactory<ClientCaregiverListResult> { public ClientCaregiverListResult Create(AuthorizationFailure failure) => ClientCaregiverListResult.Denied(failure); }
public sealed class ClientCaregiverCandidatesAuthorizationFailureFactory : IAuthorizationFailureResultFactory<ClientCaregiverCandidatesResult> { public ClientCaregiverCandidatesResult Create(AuthorizationFailure failure) => ClientCaregiverCandidatesResult.Denied(failure); }
public sealed class ClientCaregiverOperationAuthorizationFailureFactory : IAuthorizationFailureResultFactory<ClientCaregiverOperationResult> { public ClientCaregiverOperationResult Create(AuthorizationFailure failure) => ClientCaregiverOperationResult.Denied(failure); }

public sealed class ListClientCaregiversHandler(IClientCaregiverStore store) : IQueryHandler<ListClientCaregiversQuery, ClientCaregiverListResult>
{ public async ValueTask<ClientCaregiverListResult> Handle(ListClientCaregiversQuery query, CancellationToken cancellationToken) => ClientCaregiverListResult.Completed(await store.ListAsync(query.ClientId, cancellationToken)); }
public sealed class ListClientCaregiverCandidatesHandler(IClientCaregiverStore store) : IQueryHandler<ListClientCaregiverCandidatesQuery, ClientCaregiverCandidatesResult>
{ public async ValueTask<ClientCaregiverCandidatesResult> Handle(ListClientCaregiverCandidatesQuery query, CancellationToken cancellationToken) => ClientCaregiverCandidatesResult.Completed(await store.CandidatesAsync(cancellationToken)); }
public sealed class CreateClientCaregiverHandler(IClientCaregiverStore store, ICurrentActor actor, TimeProvider timeProvider) : ICommandHandler<CreateClientCaregiverCommand, ClientCaregiverOperationResult>
{ public async ValueTask<ClientCaregiverOperationResult> Handle(CreateClientCaregiverCommand command, CancellationToken cancellationToken) => ClientCaregiverOperationResult.Completed(await store.CreateAsync(new(command.ClientId, command.UserId, command.Type, command.IsPreferred, command.Services, command.StartDate, command.EndDate, command.Notes, actor.UserId!.Value, timeProvider.GetUtcNow()), cancellationToken)); }
public sealed class UpdateClientCaregiverHandler(IClientCaregiverStore store, TimeProvider timeProvider) : ICommandHandler<UpdateClientCaregiverCommand, ClientCaregiverOperationResult>
{ public async ValueTask<ClientCaregiverOperationResult> Handle(UpdateClientCaregiverCommand command, CancellationToken cancellationToken) => ClientCaregiverOperationResult.Completed(await store.UpdateAsync(new(command.ClientId, command.AssignmentId, command.Type, command.IsPreferred, command.Services, command.StartDate, command.EndDate, command.Notes, command.EditorVersion, timeProvider.GetUtcNow()), cancellationToken)); }
public sealed class EndClientCaregiverHandler(IClientCaregiverStore store, TimeProvider timeProvider) : ICommandHandler<EndClientCaregiverCommand, ClientCaregiverOperationResult>
{ public async ValueTask<ClientCaregiverOperationResult> Handle(EndClientCaregiverCommand command, CancellationToken cancellationToken) => ClientCaregiverOperationResult.Completed(await store.EndAsync(new(command.ClientId, command.AssignmentId, command.EndDate, command.EditorVersion, timeProvider.GetUtcNow()), cancellationToken)); }

public sealed class ListClientCaregiversValidator : AbstractValidator<ListClientCaregiversQuery> { public ListClientCaregiversValidator() => RuleFor(x => x.ClientId).NotEmpty(); }
public sealed class CreateClientCaregiverValidator : AbstractValidator<CreateClientCaregiverCommand>
{ public CreateClientCaregiverValidator() { RuleFor(x => x.ClientId).NotEmpty(); RuleFor(x => x.UserId).NotEmpty(); RuleFor(x => x.Type).IsInEnum(); RuleFor(x => x.StartDate).NotEmpty(); RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).When(x => x.EndDate.HasValue); RuleFor(x => x.Services).MaximumLength(ClientCaregiver.MaximumServicesLength); RuleFor(x => x.Notes).MaximumLength(ClientCaregiver.MaximumNotesLength); } }
public sealed class UpdateClientCaregiverValidator : AbstractValidator<UpdateClientCaregiverCommand>
{ public UpdateClientCaregiverValidator() { Include(new CreateShapeValidator()); RuleFor(x => x.AssignmentId).NotEmpty(); RuleFor(x => x.EditorVersion).NotEmpty().Length(64); } private sealed class CreateShapeValidator : AbstractValidator<UpdateClientCaregiverCommand> { public CreateShapeValidator() { RuleFor(x => x.ClientId).NotEmpty(); RuleFor(x => x.Type).IsInEnum(); RuleFor(x => x.StartDate).NotEmpty(); RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).When(x => x.EndDate.HasValue); RuleFor(x => x.Services).MaximumLength(ClientCaregiver.MaximumServicesLength); RuleFor(x => x.Notes).MaximumLength(ClientCaregiver.MaximumNotesLength); } } }
public sealed class EndClientCaregiverValidator : AbstractValidator<EndClientCaregiverCommand> { public EndClientCaregiverValidator() { RuleFor(x => x.ClientId).NotEmpty(); RuleFor(x => x.AssignmentId).NotEmpty(); RuleFor(x => x.EndDate).NotEmpty(); RuleFor(x => x.EditorVersion).NotEmpty().Length(64); } }
internal static class ClientCaregiverAudit
{ public static AuditEvent Create(string action, Guid clientId, ClientCaregiverOperationResult response, Guid? requestedId = null) { var succeeded = response.Mutation?.Status == ClientCaregiverMutationStatus.Succeeded; return new(action, "ClientCaregiver", (response.Mutation?.Assignment?.Id ?? requestedId ?? clientId).ToString("N"), succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed, succeeded ? AuditMetadata.Create(("clientId", clientId.ToString("N"))) : AuditMetadata.Create(("reasonCode", response.ReasonCode))); } }
