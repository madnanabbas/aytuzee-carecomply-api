using CareComply.Application.Abstractions.Authentication;
using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Domain.Clients;
using FluentValidation;
using Mediator;
using System.Linq.Expressions;

namespace CareComply.Application.Features.ClientHospitalizations;

public sealed record ListClientHospitalizationsQuery(Guid ClientId)
    : IQuery<HospitalizationResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.ClientHospitalization.View;
}

public sealed record SaveClientHospitalizationCommand(
    Guid ClientId,
    Guid? Id,
    string HospitalName,
    DateTimeOffset AdmittedAtUtc,
    DateTimeOffset? DischargedAtUtc,
    string Reason,
    string? Diagnosis,
    string? DischargeInstructions,
    string? EditorVersion)
    : ICommand<HospitalizationResult>, IRequirePermission, IAuditableRequest<HospitalizationResult>
{
    public string RequiredPermission => Id.HasValue
        ? Permissions.ClientHospitalization.Edit
        : Permissions.ClientHospitalization.Create;

    public AuditEvent CreateAuditEvent(HospitalizationResult response) => new(
        Id.HasValue ? "ClientHospitalization.Updated" : "ClientHospitalization.Created",
        "ClientHospitalization",
        (Id ?? ClientId).ToString(),
        response.Mutation?.Status == HospitalizationMutationStatus.Succeeded
            ? AuditOutcome.Succeeded
            : AuditOutcome.Failed);
}

public sealed record HospitalizationResult(
    HospitalizationList? List,
    HospitalizationMutation? Mutation,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<HospitalizationResult>
{
    public string ReasonCode =>
        AuthorizationFailure?.ToString()
        ?? Mutation?.Status.ToString()
        ?? "Succeeded";

    public static HospitalizationResult From(HospitalizationList? list) => new(list, null, null);

    public static HospitalizationResult From(HospitalizationMutation mutation) => new(null, mutation, null);

    public static HospitalizationResult Denied(AuthorizationFailure failure) => new(null, null, failure);
}


public sealed class ListClientHospitalizationsHandler(IClientHospitalizationStore store)
    : IQueryHandler<ListClientHospitalizationsQuery, HospitalizationResult>
{
    public async ValueTask<HospitalizationResult> Handle(
        ListClientHospitalizationsQuery query,
        CancellationToken cancellationToken) =>
        HospitalizationResult.From(await store.List(query.ClientId, cancellationToken));
}

public sealed class SaveClientHospitalizationHandler(
    IClientHospitalizationStore store,
    ICurrentActor actor,
    TimeProvider clock) : ICommandHandler<SaveClientHospitalizationCommand, HospitalizationResult>
{
    public async ValueTask<HospitalizationResult> Handle(
        SaveClientHospitalizationCommand command,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var mutation = command.Id.HasValue
            ? await store.Update(
                new(
                    command.ClientId,
                    command.Id.Value,
                    command.HospitalName,
                    command.AdmittedAtUtc,
                    command.DischargedAtUtc,
                    command.Reason,
                    command.Diagnosis,
                    command.DischargeInstructions,
                    command.EditorVersion ?? string.Empty,
                    now),
                cancellationToken)
            : await store.Create(
                new(
                    command.ClientId,
                    command.HospitalName,
                    command.AdmittedAtUtc,
                    command.DischargedAtUtc,
                    command.Reason,
                    command.Diagnosis,
                    command.DischargeInstructions,
                    actor.UserId!.Value,
                    now),
                cancellationToken);

        return HospitalizationResult.From(mutation);
    }
}

public abstract class ClientHospitalizationValidator<T> : AbstractValidator<T>
{
    protected void AddRules(
        Expression<Func<T, string?>> hospitalName,
        Expression<Func<T, DateTimeOffset>> admittedAtUtc,
        Expression<Func<T, DateTimeOffset?>> dischargedAtUtc,
        Expression<Func<T, string?>> reason,
        Expression<Func<T, string?>> diagnosis,
        Expression<Func<T, string?>> dischargeInstructions)
    {
        RuleFor(hospitalName)
            .NotEmpty()
            .MaximumLength(ClientHospitalization.HospitalMaxLength);
        RuleFor(reason)
            .NotEmpty()
            .MaximumLength(ClientHospitalization.ReasonMaxLength);
        RuleFor(diagnosis)
            .MaximumLength(ClientHospitalization.DetailsMaxLength);
        RuleFor(dischargeInstructions)
            .MaximumLength(ClientHospitalization.DetailsMaxLength);
        RuleFor(dischargedAtUtc)
            .GreaterThanOrEqualTo(admittedAtUtc)
            .When(command => dischargedAtUtc.Compile()(command).HasValue);
    }
}

public sealed class SaveClientHospitalizationValidator
    : ClientHospitalizationValidator<SaveClientHospitalizationCommand>
{
    public SaveClientHospitalizationValidator()
    {
        RuleFor(command => command.ClientId).NotEmpty();
        AddRules(command => command.HospitalName, command => command.AdmittedAtUtc,
            command => command.DischargedAtUtc, command => command.Reason,
            command => command.Diagnosis, command => command.DischargeInstructions);
        RuleFor(command => command.EditorVersion)
            .NotEmpty()
            .Length(64)
            .When(command => command.Id.HasValue);
    }
}
