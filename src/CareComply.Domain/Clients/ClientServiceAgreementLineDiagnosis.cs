namespace CareComply.Domain.Clients;

public sealed class ClientServiceAgreementLineDiagnosis
{
    public const int MaximumDiagnosisTextLength = 1000;
    public const int MaximumIcdCodeLength = 20;

    private ClientServiceAgreementLineDiagnosis()
    {
    }

    public Guid Id { get; private set; }
    public Guid ClientServiceAgreementLineId { get; private set; }
    public string? DiagnosisText { get; private set; }
    public string? IcdCode { get; private set; }
    public bool IsPrimary { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static ClientServiceAgreementLineDiagnosis Create(
        Guid id,
        Guid clientServiceAgreementLineId,
        string? diagnosisText,
        string? icdCode,
        bool isPrimary,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A diagnosis identifier is required.", nameof(id));
        }

        if (clientServiceAgreementLineId == Guid.Empty)
        {
            throw new ArgumentException(
                "A service agreement line identifier is required.",
                nameof(clientServiceAgreementLineId));
        }

        var timestamp = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        return new ClientServiceAgreementLineDiagnosis
        {
            Id = id,
            ClientServiceAgreementLineId = clientServiceAgreementLineId,
            DiagnosisText = NormalizeOptional(
                diagnosisText,
                MaximumDiagnosisTextLength,
                nameof(diagnosisText)),
            IcdCode = NormalizeOptional(icdCode, MaximumIcdCodeLength, nameof(icdCode)),
            IsPrimary = isPrimary,
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp,
        };
    }

    public void Update(
        string? diagnosisText,
        string? icdCode,
        bool isPrimary,
        DateTimeOffset updatedAtUtc)
    {
        DiagnosisText = NormalizeOptional(
            diagnosisText,
            MaximumDiagnosisTextLength,
            nameof(diagnosisText));
        IcdCode = NormalizeOptional(icdCode, MaximumIcdCodeLength, nameof(icdCode));
        IsPrimary = isPrimary;
        UpdatedAtUtc = EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The timestamp must be UTC.", parameterName);
        }

        return value;
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
