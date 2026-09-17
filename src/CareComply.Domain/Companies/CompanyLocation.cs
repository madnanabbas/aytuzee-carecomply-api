namespace CareComply.Domain.Companies;

public sealed class CompanyLocation
{
    public const int MaximumNameLength = 150;
    private CompanyLocation() { }
    private CompanyLocation(Guid id, Guid companyId, string name, bool isMain, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty || companyId == Guid.Empty) throw new ArgumentException("Location and company identifiers are required.");
        Id = id; CompanyId = companyId; Name = Normalize(name); IsMain = isMain; IsActive = true; CreatedAtUtc = createdAtUtc;
    }
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsMain { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public static CompanyLocation Create(Guid id, Guid companyId, string name, bool isMain, DateTimeOffset now) => new(id, companyId, name, isMain, now);
    public void Update(string name, bool isActive) { Name = Normalize(name); IsActive = isActive; }
    private static string Normalize(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); var x = value.Trim(); if (x.Length > MaximumNameLength) throw new ArgumentOutOfRangeException(nameof(value)); return x; }
}
