namespace CareComply.Domain.Clients;

public static class ClientServiceAgreementCalculations
{
    private const decimal MinutesPerHour = 60m;

    public static decimal CalculateEstimatedBudget(decimal authorizedUnits, decimal ratePerUnit) =>
        authorizedUnits * ratePerUnit;

    public static decimal CalculatePlannedHoursPerWeek(
        decimal plannedHoursPerVisit,
        decimal plannedVisitsPerWeek) =>
        plannedHoursPerVisit * plannedVisitsPerWeek;

    public static decimal CalculatePlannedUnitsPerWeek(
        decimal plannedHoursPerWeek,
        int minutesPerUnit) =>
        plannedHoursPerWeek * MinutesPerHour / EnsurePositiveMinutesPerUnit(minutesPerUnit);

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
