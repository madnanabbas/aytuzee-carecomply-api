namespace CareComply.Domain.Companies;

public sealed class Payer
{
    public const int MaximumNameLength = 200;

    private Payer() { }

    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Payer Create(Guid id, Guid companyId, string name, DateTimeOffset nowUtc)
    {
        if (id == Guid.Empty || companyId == Guid.Empty)
            throw new ArgumentException("Payer and company identifiers are required.");
        var timestamp = EnsureUtc(nowUtc, nameof(nowUtc));
        return new Payer
        {
            Id = id,
            CompanyId = companyId,
            Name = NormalizeName(name),
            IsActive = true,
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp,
        };
    }

    public void Update(string name, bool isActive, DateTimeOffset nowUtc)
    {
        Name = NormalizeName(name);
        IsActive = isActive;
        UpdatedAtUtc = EnsureUtc(nowUtc, nameof(nowUtc));
    }

    private static string NormalizeName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > MaximumNameLength)
            throw new ArgumentOutOfRangeException(nameof(value));
        return normalized;
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero) throw new ArgumentException("The timestamp must be UTC.", parameterName);
        return value;
    }
}
