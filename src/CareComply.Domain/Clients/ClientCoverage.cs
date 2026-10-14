namespace CareComply.Domain.Clients;

public sealed class ClientCoverage
{
    public const int MaximumPayerNameLength = 200;
    public const int MaximumMemberIdLength = 100;
    public const int MaximumMedicaidIdLength = 100;

    private ClientCoverage() { }

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid? PayerId { get; private set; }
    public string? ManualPayerName { get; private set; }
    public string? MemberId { get; private set; }
    public string? MedicaidId { get; private set; }
    public DateOnly? EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public bool IsVerified { get; private set; }
    public bool IsPrimary { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static ClientCoverage Create(
        Guid id, Guid clientId, Guid? payerId, string? manualPayerName, string? memberId,
        string? medicaidId, DateOnly? effectiveFrom, DateOnly? effectiveTo, bool isVerified,
        bool isPrimary, DateTimeOffset nowUtc)
    {
        if (id == Guid.Empty || clientId == Guid.Empty)
            throw new ArgumentException("Coverage and client identifiers are required.");
        var coverage = new ClientCoverage { Id = id, ClientId = clientId };
        coverage.Apply(payerId, manualPayerName, memberId, medicaidId, effectiveFrom, effectiveTo,
            isVerified, isPrimary, nowUtc);
        coverage.CreatedAtUtc = coverage.UpdatedAtUtc;
        return coverage;
    }

    public void Update(
        Guid? payerId, string? manualPayerName, string? memberId, string? medicaidId,
        DateOnly? effectiveFrom, DateOnly? effectiveTo, bool isVerified, bool isPrimary,
        DateTimeOffset nowUtc) => Apply(payerId, manualPayerName, memberId, medicaidId,
            effectiveFrom, effectiveTo, isVerified, isPrimary, nowUtc);

    private void Apply(
        Guid? payerId, string? manualPayerName, string? memberId, string? medicaidId,
        DateOnly? effectiveFrom, DateOnly? effectiveTo, bool isVerified, bool isPrimary,
        DateTimeOffset nowUtc)
    {
        var manual = Normalize(manualPayerName, MaximumPayerNameLength, nameof(manualPayerName));
        if (payerId.HasValue == (manual is not null))
            throw new ArgumentException("Select either a master payer or enter a manual payer name.");
        if (payerId == Guid.Empty) throw new ArgumentException("A valid payer identifier is required.", nameof(payerId));
        if (effectiveFrom.HasValue && effectiveTo.HasValue && effectiveTo < effectiveFrom)
            throw new ArgumentException("Effective-to date cannot be before effective-from date.", nameof(effectiveTo));

        PayerId = payerId;
        ManualPayerName = manual;
        MemberId = Normalize(memberId, MaximumMemberIdLength, nameof(memberId));
        MedicaidId = Normalize(medicaidId, MaximumMedicaidIdLength, nameof(medicaidId));
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        IsVerified = isVerified;
        IsPrimary = isPrimary;
        UpdatedAtUtc = EnsureUtc(nowUtc, nameof(nowUtc));
    }

    private static string? Normalize(string? value, int maximum, string parameterName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized)) return null;
        if (normalized.Length > maximum) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("The timestamp must be UTC.", parameterName);
        return value;
    }
}
