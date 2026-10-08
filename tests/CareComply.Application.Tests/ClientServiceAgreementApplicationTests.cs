using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Application.Features.ClientServiceAgreements;

namespace CareComply.Application.Tests;

public sealed class ClientServiceAgreementApplicationTests
{
    [Fact]
    public void RequestsDeclareCanonicalPermissions()
    {
        var clientId = Guid.NewGuid();
        var agreementId = Guid.NewGuid();

        Assert.Equal(
            Permissions.ClientServiceAgreement.View,
            new ListClientServiceAgreementsQuery(clientId).RequiredPermission);
        Assert.Equal(
            Permissions.ClientServiceAgreement.View,
            new GetClientServiceAgreementQuery(clientId, agreementId).RequiredPermission);
        Assert.Equal(
            Permissions.ClientServiceAgreement.Create,
            CreateCommand(clientId, [ValidLine()]).RequiredPermission);
        Assert.Equal(
            Permissions.ClientServiceAgreement.Edit,
            UpdateCommand(clientId, agreementId, new string('A', 64), [ValidLine()])
                .RequiredPermission);
    }

    [Fact]
    public void CreateRequiresAtLeastOneLineAndLimitsCollectionSize()
    {
        var validator = new CreateClientServiceAgreementValidator();
        var missing = validator.Validate(CreateCommand(Guid.NewGuid(), []));
        var excessive = validator.Validate(CreateCommand(
            Guid.NewGuid(),
            Enumerable.Range(0, 26).Select(_ => ValidLine()).ToArray()));

        Assert.Contains(missing.Errors, error => error.PropertyName == "Lines"
            && error.ErrorMessage == "At least one service line is required.");
        Assert.Contains(excessive.Errors, error => error.PropertyName == "Lines"
            && error.ErrorMessage == "No more than 25 service lines may be supplied.");
    }

    [Fact]
    public void ValidatorRejectsInvalidDatesValuesAndEditorVersion()
    {
        var invalidLine = ValidLine() with
        {
            ServiceCode = "",
            ServiceName = null,
            MinutesPerUnit = 0,
            AuthorizedUnits = -1,
            RatePerUnit = -1,
            PlannedHoursPerVisit = -1,
            PlannedVisitsPerWeek = -1,
        };
        var command = UpdateCommand(Guid.NewGuid(), Guid.NewGuid(), "invalid", [invalidLine]) with
        {
            StartDate = new DateOnly(2026, 2, 2),
            EndDate = new DateOnly(2026, 2, 1),
        };

        var result = new UpdateClientServiceAgreementValidator().Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "EditorVersion");
        Assert.Contains(result.Errors, error => error.PropertyName == "EndDate");
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("ServiceCode", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("ServiceName", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("MinutesPerUnit", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("AuthorizedUnits", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("RatePerUnit", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("PlannedHoursPerVisit", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("PlannedVisitsPerWeek", StringComparison.Ordinal));
    }

    [Fact]
    public void SuccessfulAuditContainsOnlySafeIdentifiersAndLineCount()
    {
        var clientId = Guid.NewGuid();
        var agreementId = Guid.NewGuid();
        var item = new ClientServiceAgreementItem(
            agreementId, "Agreement", "Payer", "Sensitive member", "Sensitive prior auth",
            new DateOnly(2026, 1, 1), null, "Active", "Sensitive notes", new string('A', 64),
            [new(Guid.NewGuid(), "T1019", "Service", null, 15, 10, 5, 2, 3,
                "Sensitive diagnosis", "Sensitive ICD", 50, 6, 24)]);
        var response = ClientServiceAgreementResult.Changed(
            new(ClientServiceAgreementMutationStatus.Succeeded, item));

        var audit = CreateCommand(clientId, [ValidLine()]).CreateAuditEvent(response);
        var metadata = audit.Metadata.Entries.ToDictionary(entry => entry.Key, entry => entry.Value);

        Assert.Equal("ClientServiceAgreement.Created", audit.Action);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(agreementId.ToString("N"), audit.ResourceId);
        Assert.Equal(clientId.ToString("N"), metadata["clientId"]);
        Assert.Equal("1", metadata["lineCount"]);
        var serialized = string.Join('|', audit.Metadata.Entries.Select(entry => $"{entry.Key}:{entry.Value}"));
        Assert.DoesNotContain("Sensitive", serialized, StringComparison.Ordinal);
    }

    private static CreateClientServiceAgreementCommand CreateCommand(
        Guid clientId,
        IReadOnlyList<ClientServiceAgreementLineInput> lines) => new(
        clientId, "Agreement", "Payer", "Member", "Prior", new DateOnly(2026, 1, 1),
        null, "Active", "Notes", lines);

    private static UpdateClientServiceAgreementCommand UpdateCommand(
        Guid clientId,
        Guid agreementId,
        string editorVersion,
        IReadOnlyList<ClientServiceAgreementLineInput> lines) => new(
        clientId, agreementId, "Agreement", "Payer", "Member", "Prior",
        new DateOnly(2026, 1, 1), null, "Active", "Notes", editorVersion, lines);

    private static ClientServiceAgreementLineInput ValidLine() => new(
        null, "T1019", "Personal care", "15 minutes", 15, 100, 8, 2, 3,
        "Diagnosis", "Z00.00");
}
