namespace CareComply.Domain.Clients;

public sealed class ClientHospitalization
{
    public const int MaximumHospitalNameLength = 200;
    public const int MaximumReasonLength = 1000;

    private ClientHospitalization()
    {
    }

    public ClientHospitalization(
        Guid id,
        Guid clientId,
        string hospitalName,
        DateTimeOffset admittedAtUtc,
        DateTimeOffset? dischargedAtUtc,
        string? reason)
    {
        if (id == Guid.Empty) throw new ArgumentException("A hospitalization identifier is required.", nameof(id));
        if (clientId == Guid.Empty) throw new ArgumentException("A client identifier is required.", nameof(clientId));
        if (admittedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Admission timestamp must be UTC.", nameof(admittedAtUtc));
        if (dischargedAtUtc.HasValue && dischargedAtUtc.Value.Offset != TimeSpan.Zero)
            throw new ArgumentException("Discharge timestamp must be UTC.", nameof(dischargedAtUtc));
        if (dischargedAtUtc.HasValue && dischargedAtUtc.Value < admittedAtUtc)
            throw new ArgumentException("Discharge cannot occur before admission.", nameof(dischargedAtUtc));

        Id = id;
        ClientId = clientId;
        HospitalName = NormalizeRequired(hospitalName, MaximumHospitalNameLength, nameof(hospitalName));
        AdmittedAtUtc = admittedAtUtc;
        DischargedAtUtc = dischargedAtUtc;
        Reason = Normalize(reason, MaximumReasonLength, nameof(reason));
    }

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public string HospitalName { get; private set; } = string.Empty;
    public DateTimeOffset AdmittedAtUtc { get; private set; }
    public DateTimeOffset? DischargedAtUtc { get; private set; }
    public string Reason { get; private set; } = string.Empty;

    public void Discharge(DateTimeOffset dischargedAtUtc)
    {
        if (dischargedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("Discharge timestamp must be UTC.", nameof(dischargedAtUtc));
        if (dischargedAtUtc < AdmittedAtUtc) throw new ArgumentOutOfRangeException(nameof(dischargedAtUtc));
        DischargedAtUtc = dischargedAtUtc;
    }

    private static string NormalizeRequired(string value, int maxLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Normalize(value, maxLength, parameterName);
    }

    private static string Normalize(string? value, int maxLength, string parameterName)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (normalized.Length > maxLength) throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }
}
