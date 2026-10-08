namespace CareComply.Domain.Clients;

public sealed class ClientServiceAgreementLine
{
    public const int MaximumServiceCodeLength = 50;
    public const int MaximumServiceNameLength = 200;
    public const int MaximumUnitTypeLength = 50;
    public const int MaximumDiagnosisTextLength = 1000;
    public const int MaximumIcdCodeLength = 20;

    private ClientServiceAgreementLine()
    {
    }

    public Guid Id { get; private set; }
    public Guid ClientServiceAgreementId { get; private set; }
    public string ServiceCode { get; private set; } = string.Empty;
    public string ServiceName { get; private set; } = string.Empty;
    public string? UnitType { get; private set; }
    public int? MinutesPerUnit { get; private set; }
    public decimal? AuthorizedUnits { get; private set; }
    public decimal? RatePerUnit { get; private set; }
    public decimal? PlannedHoursPerVisit { get; private set; }
    public decimal? PlannedVisitsPerWeek { get; private set; }
    public string? PrimaryDiagnosisText { get; private set; }
    public string? IcdCode { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static ClientServiceAgreementLine Create(
        Guid id,
        Guid clientServiceAgreementId,
        string serviceCode,
        string serviceName,
        string? unitType,
        int? minutesPerUnit,
        decimal? authorizedUnits,
        decimal? ratePerUnit,
        decimal? plannedHoursPerVisit,
        decimal? plannedVisitsPerWeek,
        string? primaryDiagnosisText,
        string? icdCode,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A service agreement line identifier is required.", nameof(id));
        }

        if (clientServiceAgreementId == Guid.Empty)
        {
            throw new ArgumentException(
                "A service agreement identifier is required.",
                nameof(clientServiceAgreementId));
        }

        if (minutesPerUnit is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minutesPerUnit),
                "Minutes per unit must be greater than zero when supplied.");
        }

        EnsureNotNegative(authorizedUnits, nameof(authorizedUnits));
        EnsureNotNegative(ratePerUnit, nameof(ratePerUnit));
        EnsureNotNegative(plannedHoursPerVisit, nameof(plannedHoursPerVisit));
        EnsureNotNegative(plannedVisitsPerWeek, nameof(plannedVisitsPerWeek));

        var timestamp = EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        return new ClientServiceAgreementLine
        {
            Id = id,
            ClientServiceAgreementId = clientServiceAgreementId,
            ServiceCode = NormalizeRequired(
                serviceCode,
                MaximumServiceCodeLength,
                nameof(serviceCode)),
            ServiceName = NormalizeRequired(
                serviceName,
                MaximumServiceNameLength,
                nameof(serviceName)),
            UnitType = NormalizeOptional(unitType, MaximumUnitTypeLength, nameof(unitType)),
            MinutesPerUnit = minutesPerUnit,
            AuthorizedUnits = authorizedUnits,
            RatePerUnit = ratePerUnit,
            PlannedHoursPerVisit = plannedHoursPerVisit,
            PlannedVisitsPerWeek = plannedVisitsPerWeek,
            PrimaryDiagnosisText = NormalizeOptional(
                primaryDiagnosisText,
                MaximumDiagnosisTextLength,
                nameof(primaryDiagnosisText)),
            IcdCode = NormalizeOptional(icdCode, MaximumIcdCodeLength, nameof(icdCode)),
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp,
        };
    }

    public void Update(
        string serviceCode,
        string serviceName,
        string? unitType,
        int? minutesPerUnit,
        decimal? authorizedUnits,
        decimal? ratePerUnit,
        decimal? plannedHoursPerVisit,
        decimal? plannedVisitsPerWeek,
        string? primaryDiagnosisText,
        string? icdCode,
        DateTimeOffset updatedAtUtc)
    {
        if (minutesPerUnit is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minutesPerUnit),
                "Minutes per unit must be greater than zero when supplied.");
        }

        EnsureNotNegative(authorizedUnits, nameof(authorizedUnits));
        EnsureNotNegative(ratePerUnit, nameof(ratePerUnit));
        EnsureNotNegative(plannedHoursPerVisit, nameof(plannedHoursPerVisit));
        EnsureNotNegative(plannedVisitsPerWeek, nameof(plannedVisitsPerWeek));

        ServiceCode = NormalizeRequired(serviceCode, MaximumServiceCodeLength, nameof(serviceCode));
        ServiceName = NormalizeRequired(serviceName, MaximumServiceNameLength, nameof(serviceName));
        UnitType = NormalizeOptional(unitType, MaximumUnitTypeLength, nameof(unitType));
        MinutesPerUnit = minutesPerUnit;
        AuthorizedUnits = authorizedUnits;
        RatePerUnit = ratePerUnit;
        PlannedHoursPerVisit = plannedHoursPerVisit;
        PlannedVisitsPerWeek = plannedVisitsPerWeek;
        PrimaryDiagnosisText = NormalizeOptional(
            primaryDiagnosisText,
            MaximumDiagnosisTextLength,
            nameof(primaryDiagnosisText));
        IcdCode = NormalizeOptional(icdCode, MaximumIcdCodeLength, nameof(icdCode));
        UpdatedAtUtc = EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
    }

    private static void EnsureNotNegative(decimal? value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Value cannot be negative.");
        }
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The timestamp must be UTC.", parameterName);
        }

        return value;
    }

    private static string NormalizeRequired(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return NormalizeOptional(value, maximumLength, parameterName)!;
    }

    private static string? NormalizeOptional(string? value, int maximumLength, string parameterName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Value cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }
}
