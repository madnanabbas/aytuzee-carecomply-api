using CareComply.Domain.Clients;

namespace CareComply.Domain.Tests;

public sealed class ClientServiceAgreementTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 8, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreatesValidAgreementAndNormalizesOptionalText()
    {
        var agreement = ClientServiceAgreement.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            " AUTH-100 ",
            " Payer ",
            " MEMBER-1 ",
            " PRIOR-1 ",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 31),
            ClientServiceAgreementStatus.PendingAuthorization,
            ClientServiceAgreementPriority.High,
            true,
            " Notes ",
            Now);

        Assert.Equal("AUTH-100", agreement.AgreementNumber);
        Assert.Equal("Payer", agreement.PayerName);
        Assert.Equal("MEMBER-1", agreement.InsuredMemberId);
        Assert.Equal("PRIOR-1", agreement.PriorAuthorizationNumber);
        Assert.Equal(ClientServiceAgreementStatus.PendingAuthorization, agreement.Status);
        Assert.Equal(ClientServiceAgreementPriority.High, agreement.Priority);
        Assert.True(agreement.IsDummy);
        Assert.Equal("Notes", agreement.Notes);
        Assert.Equal(Now, agreement.CreatedAtUtc);
        Assert.Equal(Now, agreement.UpdatedAtUtc);
    }

    [Fact]
    public void RejectsEndDateBeforeStartDate()
    {
        Assert.Throws<ArgumentException>(() => ClientServiceAgreement.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            null,
            null,
            new DateOnly(2026, 10, 2),
            new DateOnly(2026, 10, 1),
            null,
            null,
            false,
            null,
            Now));
    }

    [Fact]
    public void MultipleLinesCanReferenceOneAgreement()
    {
        var agreementId = Guid.NewGuid();
        var first = CreateLine(agreementId, serviceCode: "T1019");
        var second = CreateLine(agreementId, serviceCode: "S5125");

        Assert.Equal(agreementId, first.ClientServiceAgreementId);
        Assert.Equal(agreementId, second.ClientServiceAgreementId);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingServiceCode(string serviceCode)
    {
        Assert.Throws<ArgumentException>(() => CreateLine(Guid.NewGuid(), serviceCode: serviceCode));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingServiceName(string serviceName)
    {
        Assert.Throws<ArgumentException>(() => CreateLine(Guid.NewGuid(), serviceName: serviceName));
    }

    [Fact]
    public void RejectsNegativeAuthorizedUnits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateLine(Guid.NewGuid(), authorizedUnits: -0.0001m));
    }

    [Fact]
    public void RejectsNegativeRatePerUnit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateLine(Guid.NewGuid(), ratePerUnit: -0.0001m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonPositiveMinutesPerUnit(int minutesPerUnit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateLine(Guid.NewGuid(), minutesPerUnit: minutesPerUnit));
    }

    [Fact]
    public void RejectsNegativePlannedHoursPerVisit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateLine(Guid.NewGuid(), plannedHoursPerVisit: -0.0001m));
    }

    [Fact]
    public void RejectsNegativePlannedVisitsPerWeek()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateLine(Guid.NewGuid(), plannedVisitsPerWeek: -0.0001m));
    }

    [Fact]
    public void RejectsInvalidLineAndRateDateRangesAndNegativeAuthorizedBudget()
    {
        Assert.Throws<ArgumentException>(() => ClientServiceAgreementLine.Create(
            Guid.NewGuid(), Guid.NewGuid(), "T1019", "Service",
            ClientServiceAgreementUnitType.Hour, 60, 1, 1,
            new DateOnly(2026, 2, 2), new DateOnly(2026, 2, 1), null, null, null, null, null, Now));
        Assert.Throws<ArgumentException>(() => ClientServiceAgreementLine.Create(
            Guid.NewGuid(), Guid.NewGuid(), "T1019", "Service",
            ClientServiceAgreementUnitType.Hour, 60, 1, 1,
            new DateOnly(2026, 1, 1), null,
            new DateOnly(2026, 2, 2), new DateOnly(2026, 2, 1), null, null, null, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => ClientServiceAgreementLine.Create(
            Guid.NewGuid(), Guid.NewGuid(), "T1019", "Service",
            ClientServiceAgreementUnitType.Hour, 60, 1, 1,
            new DateOnly(2026, 1, 1), null, null, null, -1, null, null, Now));
    }

    [Fact]
    public void DiagnosisSupportsPrimaryAndAdditionalValues()
    {
        var lineId = Guid.NewGuid();
        var primary = ClientServiceAgreementLineDiagnosis.Create(
            Guid.NewGuid(), lineId, " Primary diagnosis ", " Z00.00 ", true, Now);
        var additional = ClientServiceAgreementLineDiagnosis.Create(
            Guid.NewGuid(), lineId, " Additional diagnosis ", null, false, Now);

        Assert.True(primary.IsPrimary);
        Assert.Equal("Primary diagnosis", primary.DiagnosisText);
        Assert.Equal("Z00.00", primary.IcdCode);
        Assert.False(additional.IsPrimary);
    }

    [Fact]
    public void UpdatesAgreementAndLineWithoutChangingOwnership()
    {
        var clientId = Guid.NewGuid();
        var agreement = ClientServiceAgreement.Create(
            Guid.NewGuid(), clientId, null, null, null, null,
            new DateOnly(2026, 10, 1), null, null, null, false, null, Now);
        var line = CreateLine(agreement.Id);
        var updatedAt = Now.AddHours(1);

        agreement.Update(
            "AUTH-UPDATED", "Payer", "Member", "Prior",
            new DateOnly(2026, 11, 1), new DateOnly(2027, 1, 31),
            ClientServiceAgreementStatus.Active, ClientServiceAgreementPriority.Urgent,
            false, "Updated", updatedAt);
        line.Update(
            "S5125", "Updated service", ClientServiceAgreementUnitType.Hour, 60, 25m, 12m,
            new DateOnly(2026, 11, 1), null, null, null, 300m, 4m, 2m, updatedAt);

        Assert.Equal(clientId, agreement.ClientId);
        Assert.Equal("AUTH-UPDATED", agreement.AgreementNumber);
        Assert.Equal(updatedAt, agreement.UpdatedAtUtc);
        Assert.Equal(agreement.Id, line.ClientServiceAgreementId);
        Assert.Equal("S5125", line.ServiceCode);
        Assert.Equal(updatedAt, line.UpdatedAtUtc);
    }

    private static ClientServiceAgreementLine CreateLine(
        Guid agreementId,
        string serviceCode = "T1019",
        string serviceName = "Personal care",
        int? minutesPerUnit = 15,
        decimal? authorizedUnits = 100m,
        decimal? ratePerUnit = 8m,
        decimal? plannedHoursPerVisit = 2m,
        decimal? plannedVisitsPerWeek = 3m) =>
        ClientServiceAgreementLine.Create(
            Guid.NewGuid(),
            agreementId,
            serviceCode,
            serviceName,
            ClientServiceAgreementUnitType.FifteenMinutes,
            minutesPerUnit,
            authorizedUnits,
            ratePerUnit,
            new DateOnly(2026, 10, 1),
            null,
            null,
            null,
            800m,
            plannedHoursPerVisit,
            plannedVisitsPerWeek,
            Now);
}
