namespace CareComply.Domain.Clients;

public sealed class ClientHospitalization
{
    public const int HospitalMaxLength = 200;
    public const int ReasonMaxLength = 1000;
    public const int DetailsMaxLength = 2000;

    private ClientHospitalization() { }

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public string HospitalName { get; private set; } = string.Empty;
    public DateTimeOffset AdmittedAtUtc { get; private set; }
    public DateTimeOffset? DischargedAtUtc { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string Diagnosis { get; private set; } = string.Empty;
    public string DischargeInstructions { get; private set; } = string.Empty;
    public Guid? CreatedByUserId { get; private set; }
    public Guid? CreatedByGuestInvitationId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static ClientHospitalization Create(
        Guid id,
        Guid clientId,
        string hospitalName,
        DateTimeOffset admittedAtUtc,
        DateTimeOffset? dischargedAtUtc,
        string reason,
        string? diagnosis,
        string? instructions,
        Guid? actorId,
        DateTimeOffset nowUtc,
        Guid? guestInvitationId = null
    )
    {
        if (id == Guid.Empty || clientId == Guid.Empty)
            throw new ArgumentException("Identifiers are required.");
        if (actorId.HasValue == guestInvitationId.HasValue)
            throw new ArgumentException("Exactly one hospitalization creator is required.");
        var value = new ClientHospitalization
        {
            Id = id,
            ClientId = clientId,
            CreatedByUserId = actorId,
            CreatedByGuestInvitationId = guestInvitationId,
            CreatedAtUtc = nowUtc,
        };
        value.Update(
            hospitalName,
            admittedAtUtc,
            dischargedAtUtc,
            reason,
            diagnosis,
            instructions,
            nowUtc
        );
        return value;
    }

    public void Update(
        string hospitalName,
        DateTimeOffset admittedAtUtc,
        DateTimeOffset? dischargedAtUtc,
        string reason,
        string? diagnosis,
        string? instructions,
        DateTimeOffset nowUtc
    )
    {
        if (
            admittedAtUtc.Offset != TimeSpan.Zero
            || (dischargedAtUtc.HasValue && dischargedAtUtc.Value.Offset != TimeSpan.Zero)
            || nowUtc.Offset != TimeSpan.Zero
        )
            throw new ArgumentException("Timestamps must be UTC.");
        if (dischargedAtUtc < admittedAtUtc)
            throw new ArgumentException("Discharge cannot precede admission.");
        HospitalName = Required(hospitalName, HospitalMaxLength, nameof(hospitalName));
        Reason = Required(reason, ReasonMaxLength, nameof(reason));
        Diagnosis = Optional(diagnosis, DetailsMaxLength, nameof(diagnosis));
        DischargeInstructions = Optional(instructions, DetailsMaxLength, nameof(instructions));
        AdmittedAtUtc = admittedAtUtc;
        DischargedAtUtc = dischargedAtUtc;
        UpdatedAtUtc = nowUtc;
    }

    private static string Required(string value, int max, string name)
    {
        var result = value.Trim();
        if (result.Length is 0 || result.Length > max)
            throw new ArgumentOutOfRangeException(name);
        return result;
    }

    private static string Optional(string? value, int max, string name)
    {
        var result = (value ?? string.Empty).Trim();
        if (result.Length > max)
            throw new ArgumentOutOfRangeException(name);
        return result;
    }
}
