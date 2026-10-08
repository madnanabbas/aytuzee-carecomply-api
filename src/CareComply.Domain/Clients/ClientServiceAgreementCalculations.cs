namespace CareComply.Domain.Clients;

public static class ClientServiceAgreementCalculations
{
    private const decimal MinutesPerHour = 60m;

    public static decimal CalculateRemainingUnits(decimal authorizedUnits, decimal usedUnits) =>
        authorizedUnits - usedUnits;

    public static decimal CalculateEstimatedBudget(decimal authorizedUnits, decimal ratePerUnit) =>
        authorizedUnits * ratePerUnit;

    public static decimal CalculateEstimatedUsedValue(decimal usedUnits, decimal ratePerUnit) =>
        usedUnits * ratePerUnit;

    public static decimal CalculateEstimatedRemainingValue(
        decimal remainingUnits,
        decimal ratePerUnit) =>
        remainingUnits * ratePerUnit;

    public static decimal CalculatePlannedHoursPerWeek(
        decimal plannedHoursPerVisit,
        decimal plannedVisitsPerWeek) =>
        plannedHoursPerVisit * plannedVisitsPerWeek;

    public static decimal CalculateUsedUnitsFromMinutes(
        decimal deliveredMinutes,
        int minutesPerUnit) =>
        deliveredMinutes / EnsurePositiveMinutesPerUnit(minutesPerUnit);

    public static decimal CalculatePlannedUnitsPerWeek(
        decimal plannedHoursPerWeek,
        int minutesPerUnit) =>
        plannedHoursPerWeek * MinutesPerHour / EnsurePositiveMinutesPerUnit(minutesPerUnit);

    public static decimal CalculateRemainingHours(decimal remainingUnits, int minutesPerUnit) =>
        remainingUnits * EnsurePositiveMinutesPerUnit(minutesPerUnit) / MinutesPerHour;

    private static decimal EnsurePositiveMinutesPerUnit(int minutesPerUnit)
    {
        if (minutesPerUnit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minutesPerUnit),
                "Minutes per unit must be greater than zero.");
        }

        return minutesPerUnit;
    }
}
