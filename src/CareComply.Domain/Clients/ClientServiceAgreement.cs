namespace CareComply.Domain.Clients;

public sealed class ClientServiceAgreement
{
    public const int MaximumAgreementNumberLength = 100;
    public const int MaximumPayerNameLength = 200;
    public const int MaximumInsuredMemberIdLength = 100;
    public const int MaximumPriorAuthorizationNumberLength = 100;
    public const int MaximumNotesLength = 2000;

    private ClientServiceAgreement()
    {
    }

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid? PayerId { get; private set; }
    public Guid? ClientCoverageId { get; private set; }
    public string? AgreementNumber { get; private set; }
    public string? PayerName { get; private set; }
    public string? InsuredMemberId { get; private set; }
    public string? PriorAuthorizationNumber { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public ClientServiceAgreementStatus? Status { get; private set; }
    public ClientServiceAgreementPriority? Priority { get; private set; }
    // Dummy agreements are placeholders/nonbillable and must never be treated as billable.
    public bool IsDummy { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static ClientServiceAgreement Create(
        Guid id,
        Guid clientId,
        Guid? payerId,
        Guid? clientCoverageId,
        string? agreementNumber,
        string? payerName,
        string? insuredMemberId,
        string? priorAuthorizationNumber,
        DateOnly startDate,
        DateOnly? endDate,
        ClientServiceAgreementStatus? status,
        ClientServiceAgreementPriority? priority,
        bool isDummy,
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

        if (payerId == Guid.Empty || clientCoverageId == Guid.Empty)
            throw new ArgumentException("Structured payer identifiers must be valid when supplied.");

        if (startDate == default)
        {
            throw new ArgumentOutOfRangeException(nameof(startDate), "A start date is required.");
        }

        if (endDate.HasValue && endDate.Value < startDate)
        {
            throw new ArgumentException("End date cannot be before start date.", nameof(endDate));
        }

        var timestamp = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        EnsureDefined(status, nameof(status));
        EnsureDefined(priority, nameof(priority));

        return new ClientServiceAgreement
        {
            Id = id,
            ClientId = clientId,
            PayerId = payerId,
            ClientCoverageId = clientCoverageId,
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
            Status = status,
            Priority = priority,
            IsDummy = isDummy,
            Notes = NormalizeOptional(notes, MaximumNotesLength, nameof(notes)),
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp,
        };
    }

    public static ClientServiceAgreement Create(
        Guid id, Guid clientId, string? agreementNumber, string? payerName,
        string? insuredMemberId, string? priorAuthorizationNumber, DateOnly startDate,
        DateOnly? endDate, ClientServiceAgreementStatus? status,
        ClientServiceAgreementPriority? priority, bool isDummy, string? notes,
        DateTimeOffset createdAtUtc) => Create(id, clientId, null, null, agreementNumber,
            payerName, insuredMemberId, priorAuthorizationNumber, startDate, endDate, status,
            priority, isDummy, notes, createdAtUtc);

    public void Update(
        Guid? payerId,
        Guid? clientCoverageId,
        string? agreementNumber,
        string? payerName,
        string? insuredMemberId,
        string? priorAuthorizationNumber,
        DateOnly startDate,
        DateOnly? endDate,
        ClientServiceAgreementStatus? status,
        ClientServiceAgreementPriority? priority,
        bool isDummy,
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

        EnsureDefined(status, nameof(status));
        EnsureDefined(priority, nameof(priority));

        if (payerId == Guid.Empty || clientCoverageId == Guid.Empty)
            throw new ArgumentException("Structured payer identifiers must be valid when supplied.");
        PayerId = payerId;
        ClientCoverageId = clientCoverageId;
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
        Status = status;
        Priority = priority;
        IsDummy = isDummy;
        Notes = NormalizeOptional(notes, MaximumNotesLength, nameof(notes));
        UpdatedAtUtc = EnsureUtc(updatedAtUtc, nameof(updatedAtUtc));
    }

    public void Update(
        string? agreementNumber, string? payerName, string? insuredMemberId,
        string? priorAuthorizationNumber, DateOnly startDate, DateOnly? endDate,
        ClientServiceAgreementStatus? status, ClientServiceAgreementPriority? priority,
        bool isDummy, string? notes, DateTimeOffset updatedAtUtc) => Update(PayerId,
            ClientCoverageId, agreementNumber, payerName, insuredMemberId,
            priorAuthorizationNumber, startDate, endDate, status, priority, isDummy, notes,
            updatedAtUtc);

    private static DateTimeOffset EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The timestamp must be UTC.", parameterName);
        }

        return value;
    }

    private static void EnsureDefined<TEnum>(TEnum? value, string parameterName)
        where TEnum : struct, Enum
    {
        if (value.HasValue && !Enum.IsDefined(value.Value))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
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
