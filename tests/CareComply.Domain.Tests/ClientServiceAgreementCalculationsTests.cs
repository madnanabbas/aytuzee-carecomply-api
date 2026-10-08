using CareComply.Domain.Clients;

namespace CareComply.Domain.Tests;

public sealed class ClientServiceAgreementCalculationsTests
{
    [Fact]
    public void CalculatesApprovedMinuteBasedExample()
    {
        var usedUnits = ClientServiceAgreementCalculations.CalculateUsedUnitsFromMinutes(360m, 15);
        var remainingUnits = ClientServiceAgreementCalculations.CalculateRemainingUnits(100m, usedUnits);
        var plannedHours = ClientServiceAgreementCalculations.CalculatePlannedHoursPerWeek(2m, 3m);

        Assert.Equal(24m, usedUnits);
        Assert.Equal(76m, remainingUnits);
        Assert.Equal(800m, ClientServiceAgreementCalculations.CalculateEstimatedBudget(100m, 8m));
        Assert.Equal(192m, ClientServiceAgreementCalculations.CalculateEstimatedUsedValue(usedUnits, 8m));
        Assert.Equal(
            608m,
            ClientServiceAgreementCalculations.CalculateEstimatedRemainingValue(remainingUnits, 8m));
        Assert.Equal(6m, plannedHours);
        Assert.Equal(
            24m,
            ClientServiceAgreementCalculations.CalculatePlannedUnitsPerWeek(plannedHours, 15));
        Assert.Equal(
            19m,
            ClientServiceAgreementCalculations.CalculateRemainingHours(remainingUnits, 15));
    }

    [Fact]
    public void ZeroInputsProduceZeroResults()
    {
        Assert.Equal(0m, ClientServiceAgreementCalculations.CalculateRemainingUnits(0m, 0m));
        Assert.Equal(0m, ClientServiceAgreementCalculations.CalculateEstimatedBudget(0m, 0m));
        Assert.Equal(0m, ClientServiceAgreementCalculations.CalculateEstimatedUsedValue(0m, 0m));
        Assert.Equal(0m, ClientServiceAgreementCalculations.CalculateEstimatedRemainingValue(0m, 0m));
        Assert.Equal(0m, ClientServiceAgreementCalculations.CalculatePlannedHoursPerWeek(0m, 0m));
        Assert.Equal(0m, ClientServiceAgreementCalculations.CalculateUsedUnitsFromMinutes(0m, 15));
        Assert.Equal(0m, ClientServiceAgreementCalculations.CalculatePlannedUnitsPerWeek(0m, 15));
        Assert.Equal(0m, ClientServiceAgreementCalculations.CalculateRemainingHours(0m, 15));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void MinuteBasedCalculationsRejectNonPositiveMinutesPerUnit(int minutesPerUnit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ClientServiceAgreementCalculations.CalculateUsedUnitsFromMinutes(1m, minutesPerUnit));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ClientServiceAgreementCalculations.CalculatePlannedUnitsPerWeek(1m, minutesPerUnit));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ClientServiceAgreementCalculations.CalculateRemainingHours(1m, minutesPerUnit));
    }

    [Fact]
    public void DoesNotApplyHiddenRounding()
    {
        var expected = 1m / 3m;

        Assert.Equal(
            expected,
            ClientServiceAgreementCalculations.CalculateUsedUnitsFromMinutes(1m, 3));
    }
}
