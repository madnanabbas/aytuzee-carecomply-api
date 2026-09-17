namespace CareComply.Domain.Clients;

public enum ClientCaregiverType { Primary = 1, Backup = 2 }
public enum ClientCaregiverStatus { Active = 1, Ended = 2 }

public sealed class ClientCaregiver
{
    public const int MaximumServicesLength = 500;
    public const int MaximumNotesLength = 1000;
    public const int MaximumRelationshipLength = 60;
    private ClientCaregiver() { }

    public static ClientCaregiver Create(Guid id, Guid clientId, Guid userId, ClientCaregiverType type,
        bool isPreferred, string? services, DateOnly startDate, DateOnly? endDate, string? notes,
        Guid actorUserId, DateTimeOffset nowUtc, string? relationshipToClient = null, bool? livesWithClient = null)
    {
        ValidateIds(id, clientId, userId, actorUserId); ValidateDates(startDate, endDate); ValidateUtc(nowUtc);
        return new ClientCaregiver { Id = id, ClientId = clientId, UserId = userId, Type = type,
            IsPreferred = isPreferred, Services = Normalize(services, MaximumServicesLength, nameof(services)),
            StartDate = startDate, EndDate = endDate, Notes = Normalize(notes, MaximumNotesLength, nameof(notes)),
            RelationshipToClient = Normalize(relationshipToClient, MaximumRelationshipLength, nameof(relationshipToClient)),
            LivesWithClient = livesWithClient,
            Status = ClientCaregiverStatus.Active,
            CreatedByUserId = actorUserId, CreatedAtUtc = nowUtc, UpdatedAtUtc = nowUtc };
    }

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid UserId { get; private set; }
    public ClientCaregiverType Type { get; private set; }
    public bool IsPreferred { get; private set; }
    public string Services { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public string Notes { get; private set; } = string.Empty;
    public string RelationshipToClient { get; private set; } = string.Empty;
    public bool? LivesWithClient { get; private set; }
    public ClientCaregiverStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void Update(ClientCaregiverType type, bool isPreferred, string? services, DateOnly startDate,
        DateOnly? endDate, string? notes, DateTimeOffset nowUtc, string? relationshipToClient = null, bool? livesWithClient = null)
    {
        ValidateDates(startDate, endDate); ValidateUtc(nowUtc);
        var currentStatus = Status;
        Type = type; IsPreferred = isPreferred; Services = Normalize(services, MaximumServicesLength, nameof(services));
        StartDate = startDate; EndDate = endDate; Notes = Normalize(notes, MaximumNotesLength, nameof(notes));
        RelationshipToClient = Normalize(relationshipToClient, MaximumRelationshipLength, nameof(relationshipToClient));
        LivesWithClient = livesWithClient;
        Status = currentStatus; UpdatedAtUtc = nowUtc;
    }

    public void End(DateOnly endDate, DateTimeOffset nowUtc)
    {
        if (Status == ClientCaregiverStatus.Ended) throw new InvalidOperationException("Assignment is already ended.");
        ValidateDates(StartDate, endDate); ValidateUtc(nowUtc);
        if (endDate > DateOnly.FromDateTime(nowUtc.UtcDateTime))
            throw new ArgumentException("An assignment cannot be ended with a future date.", nameof(endDate));
        EndDate = endDate; Status = ClientCaregiverStatus.Ended; UpdatedAtUtc = nowUtc;
    }

    private static void ValidateIds(params Guid[] values) { if (values.Any(x => x == Guid.Empty)) throw new ArgumentException("Identifiers are required."); }
    private static void ValidateDates(DateOnly start, DateOnly? end) { if (end.HasValue && end < start) throw new ArgumentException("End date cannot precede start date."); }
    private static void ValidateUtc(DateTimeOffset value) { if (value.Offset != TimeSpan.Zero) throw new ArgumentException("Timestamp must be UTC."); }
    private static string Normalize(string? value, int maximum, string name) { var result = (value ?? string.Empty).Trim(); if (result.Length > maximum) throw new ArgumentOutOfRangeException(name); return result; }
}
