namespace CareComply.Domain.Companies;

public sealed class Company
{
    public const int MaximumNameLength = 200;
    public const int MaximumCodeLength = 50;

    private Company()
    {
    }

    private Company(Guid id, string name, string code, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("A company identifier is required.", nameof(id));
        Id = id;
        Name = NormalizeRequired(name, MaximumNameLength, nameof(name));
        Code = NormalizeRequired(code, MaximumCodeLength, nameof(code)).ToUpperInvariant();
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Company Create(Guid id, string name, string code, DateTimeOffset createdAtUtc) =>
        new(id, name, code, createdAtUtc);

    public void Update(string name, string code, bool isActive, DateTimeOffset updatedAtUtc)
    {
        Name = NormalizeRequired(name, MaximumNameLength, nameof(name));
        Code = NormalizeRequired(code, MaximumCodeLength, nameof(code)).ToUpperInvariant();
        IsActive = isActive;
        UpdatedAtUtc = updatedAtUtc;
    }

    private static string NormalizeRequired(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }
}
