using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Application.Features.Clients;
using CareComply.Domain.Clients;
using FluentValidation;
using Mediator;

namespace CareComply.Application.Features.ClientServiceAgreements;

public sealed record ListClientServiceAgreementsQuery(Guid ClientId)
    : IQuery<ClientServiceAgreementResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.ClientServiceAgreement.View;
}

public sealed record GetClientServiceAgreementQuery(Guid ClientId, Guid AgreementId)
    : IQuery<ClientServiceAgreementResult>, IRequirePermission
{
    public string RequiredPermission => Permissions.ClientServiceAgreement.View;
}

public sealed record CreateClientServiceAgreementCommand(
    Guid ClientId,
    string? AgreementNumber,
    string? PayerName,
    string? InsuredMemberId,
    string? PriorAuthorizationNumber,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Status,
    string? Notes,
    IReadOnlyList<ClientServiceAgreementLineInput>? Lines)
    : ICommand<ClientServiceAgreementResult>, IRequirePermission,
    IAuditableRequest<ClientServiceAgreementResult>
{
    public string RequiredPermission => Permissions.ClientServiceAgreement.Create;

    public AuditEvent CreateAuditEvent(ClientServiceAgreementResult response) =>
        ClientServiceAgreementAudit.Create(
            "ClientServiceAgreement.Created",
            ClientId,
            response,
            null,
            Lines?.Count ?? 0);
}

public sealed record UpdateClientServiceAgreementCommand(
    Guid ClientId,
    Guid AgreementId,
    string? AgreementNumber,
    string? PayerName,
    string? InsuredMemberId,
    string? PriorAuthorizationNumber,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Status,
    string? Notes,
    string? EditorVersion,
    IReadOnlyList<ClientServiceAgreementLineInput>? Lines)
    : ICommand<ClientServiceAgreementResult>, IRequirePermission,
    IAuditableRequest<ClientServiceAgreementResult>
{
    public string RequiredPermission => Permissions.ClientServiceAgreement.Edit;

    public AuditEvent CreateAuditEvent(ClientServiceAgreementResult response) =>
        ClientServiceAgreementAudit.Create(
            "ClientServiceAgreement.Updated",
            ClientId,
            response,
            AgreementId,
            Lines?.Count ?? 0);
}

public sealed record ClientServiceAgreementLineInput(
    Guid? Id,
    string? ServiceCode,
    string? ServiceName,
    string? UnitType,
    int? MinutesPerUnit,
    decimal? AuthorizedUnits,
    decimal? RatePerUnit,
    decimal? PlannedHoursPerVisit,
    decimal? PlannedVisitsPerWeek,
    string? PrimaryDiagnosisText,
    string? IcdCode);

public sealed record ClientServiceAgreementResult(
    IReadOnlyList<ClientServiceAgreementItem>? Items,
    ClientServiceAgreementItem? Item,
    ClientServiceAgreementMutationStatus? Status,
    AuthorizationFailure? AuthorizationFailure)
    : IAuthorizationFailureResponse<ClientServiceAgreementResult>
{
    public string ReasonCode =>
        AuthorizationFailure?.ToString()
        ?? Status?.ToString()
        ?? (Items is null && Item is null ? "NotFound" : "Succeeded");

    public static ClientServiceAgreementResult Listed(
        IReadOnlyList<ClientServiceAgreementItem>? items) => new(items, null, null, null);

    public static ClientServiceAgreementResult Found(ClientServiceAgreementItem? item) =>
        new(null, item, item is null ? ClientServiceAgreementMutationStatus.NotFound : null, null);

    public static ClientServiceAgreementResult Changed(ClientServiceAgreementMutation mutation) =>
        new(null, mutation.Item, mutation.Status, null);

    public static ClientServiceAgreementResult Denied(AuthorizationFailure failure) =>
        new(null, null, null, failure);
}

public sealed class ListClientServiceAgreementsHandler(IClientServiceAgreementStore store)
    : IQueryHandler<ListClientServiceAgreementsQuery, ClientServiceAgreementResult>
{
    public async ValueTask<ClientServiceAgreementResult> Handle(
        ListClientServiceAgreementsQuery query,
        CancellationToken cancellationToken) =>
        ClientServiceAgreementResult.Listed(
            await store.ListAsync(query.ClientId, cancellationToken));
}

public sealed class GetClientServiceAgreementHandler(IClientServiceAgreementStore store)
    : IQueryHandler<GetClientServiceAgreementQuery, ClientServiceAgreementResult>
{
    public async ValueTask<ClientServiceAgreementResult> Handle(
        GetClientServiceAgreementQuery query,
        CancellationToken cancellationToken) =>
        ClientServiceAgreementResult.Found(
            await store.GetAsync(query.ClientId, query.AgreementId, cancellationToken));
}

public sealed class CreateClientServiceAgreementHandler(
    IClientServiceAgreementStore store,
    TimeProvider timeProvider)
    : ICommandHandler<CreateClientServiceAgreementCommand, ClientServiceAgreementResult>
{
    public async ValueTask<ClientServiceAgreementResult> Handle(
        CreateClientServiceAgreementCommand command,
        CancellationToken cancellationToken) =>
        ClientServiceAgreementResult.Changed(
            await store.CreateAsync(
                new(
                    command.ClientId,
                    command.AgreementNumber,
                    command.PayerName,
                    command.InsuredMemberId,
                    command.PriorAuthorizationNumber,
                    command.StartDate,
                    command.EndDate,
                    command.Status,
                    command.Notes,
                    command.Lines!.Select(Map).ToArray(),
                    timeProvider.GetUtcNow()),
                cancellationToken));

    private static SaveClientServiceAgreementLine Map(ClientServiceAgreementLineInput line) => new(
        null,
        line.ServiceCode!,
        line.ServiceName!,
        line.UnitType,
        line.MinutesPerUnit,
        line.AuthorizedUnits,
        line.RatePerUnit,
        line.PlannedHoursPerVisit,
        line.PlannedVisitsPerWeek,
        line.PrimaryDiagnosisText,
        line.IcdCode);
}

public sealed class UpdateClientServiceAgreementHandler(
    IClientServiceAgreementStore store,
    TimeProvider timeProvider)
    : ICommandHandler<UpdateClientServiceAgreementCommand, ClientServiceAgreementResult>
{
    public async ValueTask<ClientServiceAgreementResult> Handle(
        UpdateClientServiceAgreementCommand command,
        CancellationToken cancellationToken) =>
        ClientServiceAgreementResult.Changed(
            await store.UpdateAsync(
                new(
                    command.ClientId,
                    command.AgreementId,
                    command.AgreementNumber,
                    command.PayerName,
                    command.InsuredMemberId,
                    command.PriorAuthorizationNumber,
                    command.StartDate,
                    command.EndDate,
                    command.Status,
                    command.Notes,
                    command.EditorVersion!,
                    command.Lines!.Select(Map).ToArray(),
                    timeProvider.GetUtcNow()),
                cancellationToken));

    private static SaveClientServiceAgreementLine Map(ClientServiceAgreementLineInput line) => new(
        line.Id,
        line.ServiceCode!,
        line.ServiceName!,
        line.UnitType,
        line.MinutesPerUnit,
        line.AuthorizedUnits,
        line.RatePerUnit,
        line.PlannedHoursPerVisit,
        line.PlannedVisitsPerWeek,
        line.PrimaryDiagnosisText,
        line.IcdCode);
}

public abstract class ClientServiceAgreementValidator<T> : AbstractValidator<T>
{
    protected void AddHeaderRules(
        Func<T, Guid> clientId,
        Func<T, string?> agreementNumber,
        Func<T, string?> payerName,
        Func<T, string?> insuredMemberId,
        Func<T, string?> priorAuthorizationNumber,
        Func<T, DateOnly> startDate,
        Func<T, DateOnly?> endDate,
        Func<T, string?> status,
        Func<T, string?> notes,
        Func<T, IReadOnlyList<ClientServiceAgreementLineInput>?> lines,
        bool requireLines)
    {
        RuleFor(value => clientId(value)).NotEmpty().OverridePropertyName("ClientId");
        Maximum(agreementNumber, "AgreementNumber", ClientServiceAgreement.MaximumAgreementNumberLength);
        Maximum(payerName, "PayerName", ClientServiceAgreement.MaximumPayerNameLength);
        Maximum(insuredMemberId, "InsuredMemberId", ClientServiceAgreement.MaximumInsuredMemberIdLength);
        Maximum(
            priorAuthorizationNumber,
            "PriorAuthorizationNumber",
            ClientServiceAgreement.MaximumPriorAuthorizationNumberLength);
        Maximum(status, "Status", ClientServiceAgreement.MaximumStatusLength);
        Maximum(notes, "Notes", ClientServiceAgreement.MaximumNotesLength);
        RuleFor(value => startDate(value)).NotEmpty().OverridePropertyName("StartDate");
        RuleFor(value => endDate(value))
            .GreaterThanOrEqualTo(value => startDate(value))
            .OverridePropertyName("EndDate")
            .When(value => endDate(value).HasValue);

        var lineRule = RuleFor(value => lines(value))
            .NotNull()
            .OverridePropertyName("Lines")
            .Must(value => value is null || value.Count <= ClientValidation.MaximumServices)
            .WithMessage($"No more than {ClientValidation.MaximumServices} service lines may be supplied.");
        if (requireLines)
        {
            lineRule.Must(value => value is { Count: > 0 })
                .WithMessage("At least one service line is required.");
        }

    }

    private void Maximum(Func<T, string?> value, string propertyName, int maximumLength) =>
        RuleFor(item => value(item))
            .MaximumLength(maximumLength)
            .OverridePropertyName(propertyName);
}

public sealed class CreateClientServiceAgreementValidator
    : ClientServiceAgreementValidator<CreateClientServiceAgreementCommand>
{
    public CreateClientServiceAgreementValidator()
    {
        AddHeaderRules(
            value => value.ClientId,
            value => value.AgreementNumber,
            value => value.PayerName,
            value => value.InsuredMemberId,
            value => value.PriorAuthorizationNumber,
            value => value.StartDate,
            value => value.EndDate,
            value => value.Status,
            value => value.Notes,
            value => value.Lines,
            requireLines: true);
        RuleForEach(value => value.Lines!)
            .SetValidator(new ClientServiceAgreementLineInputValidator())
            .When(value => value.Lines is not null);
    }
}

public sealed class UpdateClientServiceAgreementValidator
    : ClientServiceAgreementValidator<UpdateClientServiceAgreementCommand>
{
    public UpdateClientServiceAgreementValidator()
    {
        RuleFor(value => value.AgreementId).NotEmpty();
        RuleFor(value => value.EditorVersion)
            .Must(ClientValidation.IsValidEditorVersion)
            .WithMessage("EditorVersion must be a valid opaque version value.");
        RuleFor(value => value.Lines)
            .Must(lines => lines is null
                || lines.Where(line => line.Id.HasValue).Select(line => line.Id).Distinct().Count()
                    == lines.Count(line => line.Id.HasValue))
            .WithMessage("A service line cannot be supplied more than once.");
        AddHeaderRules(
            value => value.ClientId,
            value => value.AgreementNumber,
            value => value.PayerName,
            value => value.InsuredMemberId,
            value => value.PriorAuthorizationNumber,
            value => value.StartDate,
            value => value.EndDate,
            value => value.Status,
            value => value.Notes,
            value => value.Lines,
            requireLines: false);
        RuleForEach(value => value.Lines!)
            .SetValidator(new ClientServiceAgreementLineInputValidator())
            .When(value => value.Lines is not null);
    }
}

public sealed class ClientServiceAgreementLineInputValidator
    : AbstractValidator<ClientServiceAgreementLineInput>
{
    public ClientServiceAgreementLineInputValidator()
    {
        RuleFor(value => value.ServiceCode)
            .NotEmpty()
            .MaximumLength(ClientServiceAgreementLine.MaximumServiceCodeLength);
        RuleFor(value => value.ServiceName)
            .NotEmpty()
            .MaximumLength(ClientServiceAgreementLine.MaximumServiceNameLength);
        RuleFor(value => value.UnitType)
            .MaximumLength(ClientServiceAgreementLine.MaximumUnitTypeLength);
        RuleFor(value => value.MinutesPerUnit).GreaterThan(0).When(value => value.MinutesPerUnit.HasValue);
        RuleFor(value => value.AuthorizedUnits).GreaterThanOrEqualTo(0)
            .When(value => value.AuthorizedUnits.HasValue);
        RuleFor(value => value.RatePerUnit).GreaterThanOrEqualTo(0)
            .When(value => value.RatePerUnit.HasValue);
        RuleFor(value => value.PlannedHoursPerVisit).GreaterThanOrEqualTo(0)
            .When(value => value.PlannedHoursPerVisit.HasValue);
        RuleFor(value => value.PlannedVisitsPerWeek).GreaterThanOrEqualTo(0)
            .When(value => value.PlannedVisitsPerWeek.HasValue);
        RuleFor(value => value.PrimaryDiagnosisText)
            .MaximumLength(ClientServiceAgreementLine.MaximumDiagnosisTextLength);
        RuleFor(value => value.IcdCode)
            .MaximumLength(ClientServiceAgreementLine.MaximumIcdCodeLength);
    }
}

internal static class ClientServiceAgreementAudit
{
    public static AuditEvent Create(
        string action,
        Guid clientId,
        ClientServiceAgreementResult response,
        Guid? requestedId,
        int requestedLineCount)
    {
        var succeeded = response.Status == ClientServiceAgreementMutationStatus.Succeeded
            && response.Item is not null;
        return new(
            action,
            "ClientServiceAgreement",
            (response.Item?.Id ?? requestedId)?.ToString("N") ?? "not-created",
            succeeded ? AuditOutcome.Succeeded : AuditOutcome.Failed,
            succeeded
                ? AuditMetadata.Create(
                    ("clientId", clientId.ToString("N")),
                    ("lineCount", response.Item!.Lines.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                : AuditMetadata.Create(
                    ("reasonCode", response.ReasonCode),
                    ("requestedLineCount", requestedLineCount.ToString(
                        System.Globalization.CultureInfo.InvariantCulture))));
    }
}
