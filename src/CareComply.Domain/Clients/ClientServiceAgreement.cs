namespace CareComply.Domain.Clients;

public sealed class ClientServiceAgreement
{
    public const int MaximumAgreementNumberLength = 100;
    public const int MaximumPayerNameLength = 200;
    public const int MaximumInsuredMemberIdLength = 100;
    public const int MaximumPriorAuthorizationNumberLength = 100;
    public const int MaximumStatusLength = 50;
    public const int MaximumNotesLength = 2000;

    private ClientServiceAgreement()
    {
    }

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public string? AgreementNumber { get; private set; }
    public string? PayerName { get; private set; }
    public string? InsuredMemberId { get; private set; }
    public string? PriorAuthorizationNumber { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public string? Status { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static ClientServiceAgreement Create(
        Guid id,
        Guid clientId,
        string? agreementNumber,
        string? payerName,
        string? insuredMemberId,
        string? priorAuthorizationNumber,
        DateOnly startDate,
        DateOnly? endDate,
        string? status,
        string? notes,
        DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A service agreement identifier is required.", nameof(id));
        }

        if (clientId == Guid.Empty)
        {
            throw new ArgumentException("A client identifier is required.", nameof(clientId));
        }

        if (startDate == default)
        {
            throw new ArgumentOutOfRangeException(nameof(startDate), "A start date is required.");
        }

        if (endDate.HasValue && endDate.Value < startDate)
        {
            throw new ArgumentException("End date cannot be before start date.", nameof(endDate));
        }

        var timestamp = EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        return new ClientServiceAgreement
        {
            Id = id,
            ClientId = clientId,
            AgreementNumber = NormalizeOptional(
                agreementNumber,
                MaximumAgreementNumberLength,
                nameof(agreementNumber)),
            PayerName = NormalizeOptional(payerName, MaximumPayerNameLength, nameof(payerName)),
            InsuredMemberId = NormalizeOptional(
                insuredMemberId,
                MaximumInsuredMemberIdLength,
                nameof(insuredMemberId)),
            PriorAuthorizationNumber = NormalizeOptional(
                priorAuthorizationNumber,
                MaximumPriorAuthorizationNumberLength,
                nameof(priorAuthorizationNumber)),
            StartDate = startDate,
            EndDate = endDate,
            Status = NormalizeOptional(status, MaximumStatusLength, nameof(status)),
            Notes = NormalizeOptional(notes, MaximumNotesLength, nameof(notes)),
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp,
        };
    }

    public void Update(
        string? agreementNumber,
        string? payerName,
        string? insuredMemberId,
        string? priorAuthorizationNumber,
        DateOnly startDate,
        DateOnly? endDate,
        string? status,
        string? notes,
        DateTimeOffset updatedAtUtc)
    {
        if (startDate == default)
        {
            throw new ArgumentOutOfRangeException(nameof(startDate), "A start date is required.");
        }

        if (endDate.HasValue && endDate.Value < startDate)
        {
            throw new ArgumentException("End date cannot be before start date.", nameof(endDate));
        }

        AgreementNumber = NormalizeOptional(
            agreementNumber,
            MaximumAgreementNumberLength,
            nameof(agreementNumber));
        PayerName = NormalizeOptional(payerName, MaximumPayerNameLength, nameof(payerName));
        InsuredMemberId = NormalizeOptional(
            insuredMemberId,
            MaximumInsuredMemberIdLength,
            nameof(insuredMemberId));
        PriorAuthorizationNumber = NormalizeOptional(
            priorAuthorizationNumber,
            MaximumPriorAuthorizationNumberLength,
            nameof(priorAuthorizationNumber));
        StartDate = startDate;
        EndDate = endDate;
        Status = NormalizeOptional(status, MaximumStatusLength, nameof(status));
        Notes = NormalizeOptional(notes, MaximumNotesLength, nameof(notes));
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
