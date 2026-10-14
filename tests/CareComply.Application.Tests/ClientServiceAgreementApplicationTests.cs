using CareComply.Application.Abstractions.Clients;
using CareComply.Application.Auditing;
using CareComply.Application.Authorization;
using CareComply.Application.Features.ClientServiceAgreements;
using CareComply.Domain.Clients;

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
    public void ValidatorRejectsUnsupportedControlledValuesAndMultiplePrimaryDiagnoses()
    {
        var line = ValidLine() with
        {
            UnitType = "hour",
            EndDate = new DateOnly(2025, 12, 31),
            RateEffectiveFrom = new DateOnly(2026, 2, 1),
            RateEffectiveTo = new DateOnly(2026, 1, 31),
            Diagnoses =
            [
                new(null, "One", "A", true),
                new(null, "Two", "B", true),
            ],
        };
        var command = CreateCommand(Guid.NewGuid(), [line]) with
        {
            Status = "active",
            Priority = "Critical",
        };

        var result = new CreateClientServiceAgreementValidator().Validate(command);

        Assert.Contains(result.Errors, error => error.PropertyName == "Status");
        Assert.Contains(result.Errors, error => error.PropertyName == "Priority");
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("UnitType", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("EndDate", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.PropertyName.EndsWith("RateEffectiveTo", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.ErrorMessage ==
            "Only one primary diagnosis may be supplied per service line.");
    }

    [Fact]
    public void ValidatorAcceptsAllApprovedControlledValuesAndZeroDiagnoses()
    {
        var statuses = Enum.GetNames<ClientServiceAgreementStatus>();
        var priorities = Enum.GetNames<ClientServiceAgreementPriority>();
        var unitTypes = Enum.GetNames<ClientServiceAgreementUnitType>();

        foreach (var status in statuses)
            foreach (var priority in priorities)
                foreach (var unitType in unitTypes)
                {
                    var command = CreateCommand(Guid.NewGuid(),
                        [ValidLine() with { UnitType = unitType, Diagnoses = [] }]) with
                    {
                        Status = status,
                        Priority = priority,
                    };

                    Assert.True(new CreateClientServiceAgreementValidator().Validate(command).IsValid);
                }
    }

    [Fact]
    public void SuccessfulAuditContainsOnlySafeIdentifiersAndLineCount()
    {
        var clientId = Guid.NewGuid();
        var agreementId = Guid.NewGuid();
        var item = new ClientServiceAgreementItem(
            agreementId, "Agreement", "Payer", "Sensitive member", "Sensitive prior auth",
            new DateOnly(2026, 1, 1), null, ClientServiceAgreementStatus.Active,
            ClientServiceAgreementPriority.High, false, "Sensitive notes", new string('A', 64),
            [new(Guid.NewGuid(), "T1019", "Service", ClientServiceAgreementUnitType.FifteenMinutes,
                15, 10, 5, new DateOnly(2026, 1, 1), null, null, null, 500, 2, 3,
                [new(Guid.NewGuid(), "Sensitive diagnosis", "Sensitive ICD", true)], 50, 6, 24)]);
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
        null, "Active", "High", false, "Notes", lines);

    private static UpdateClientServiceAgreementCommand UpdateCommand(
        Guid clientId,
        Guid agreementId,
        string editorVersion,
        IReadOnlyList<ClientServiceAgreementLineInput> lines) => new(
        clientId, agreementId, "Agreement", "Payer", "Member", "Prior",
        new DateOnly(2026, 1, 1), null, "Active", "High", false, "Notes", editorVersion, lines);

    private static ClientServiceAgreementLineInput ValidLine() => new(
        null, "T1019", "Personal care", "FifteenMinutes", 15, 100, 8,
        new DateOnly(2026, 1, 1), null, null, null, 800, 2, 3,
        [new(null, "Diagnosis", "Z00.00", true)]);
}
