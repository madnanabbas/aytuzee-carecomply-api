namespace CareComply.Domain.Clients;

public sealed class ClientService
{
    public const int MaximumCodeLength = 50;
    public const int MaximumNameLength = 150;

    private ClientService()
    {
    }

    public ClientService(Guid id, Guid clientId, string code, string name, DateOnly startDate)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(clientId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(startDate, default);

        Id = id;
        ClientId = clientId;
        Code = NormalizeRequired(code, MaximumCodeLength, nameof(code));
        Name = NormalizeRequired(name, MaximumNameLength, nameof(name));
        StartDate = startDate;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public bool IsActive { get; private set; }

    public void End(DateOnly endDate)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(endDate, StartDate);
        EndDate = endDate;
        IsActive = false;
    }

    public void Rename(string name) =>
        Name = NormalizeRequired(name, MaximumNameLength, nameof(name));

    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > maxLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }
}
