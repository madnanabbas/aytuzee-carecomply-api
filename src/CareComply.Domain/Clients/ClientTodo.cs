namespace CareComply.Domain.Clients;

public enum ClientTodoPriority { Low = 1, Normal = 2, High = 3, Urgent = 4 }
public enum ClientTodoStatus { Open = 1, InProgress = 2, Completed = 3, Cancelled = 4 }

public sealed class ClientTodo
{
    public const int MaximumTitleLength = 200;
    public const int MaximumDescriptionLength = 4000;

    private ClientTodo() { }

    private ClientTodo(Guid id, Guid clientId, string title, string? description,
        Guid assignedToUserId, DateOnly? dueDate, ClientTodoPriority priority,
        Guid createdByUserId, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("A todo identifier is required.", nameof(id));
        if (clientId == Guid.Empty) throw new ArgumentException("A client identifier is required.", nameof(clientId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("A creator is required.", nameof(createdByUserId));
        Id = id;
        ClientId = clientId;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        UpdatedAtUtc = CreatedAtUtc;
        Status = ClientTodoStatus.Open;
        SetDetails(title, description, assignedToUserId, dueDate, priority);
    }

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Guid AssignedToUserId { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public ClientTodoPriority Priority { get; private set; }
    public ClientTodoStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid? CompletedByUserId { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static ClientTodo Create(Guid id, Guid clientId, string title, string? description,
        Guid assignedToUserId, DateOnly? dueDate, ClientTodoPriority priority,
        Guid createdByUserId, DateTimeOffset createdAtUtc) =>
        new(id, clientId, title, description, assignedToUserId, dueDate, priority, createdByUserId, createdAtUtc);

    public void UpdateDetails(string title, string? description, Guid assignedToUserId,
        DateOnly? dueDate, ClientTodoPriority priority, DateTimeOffset updatedAtUtc)
    {
        EnsureEditable();
        SetDetails(title, description, assignedToUserId, dueDate, priority);
        Touch(updatedAtUtc);
    }

    public void StartProgress(DateTimeOffset updatedAtUtc)
    {
        if (Status != ClientTodoStatus.Open) throw new InvalidOperationException("Only an open todo can be started.");
        Status = ClientTodoStatus.InProgress;
        Touch(updatedAtUtc);
    }

    public void Complete(Guid completedByUserId, DateTimeOffset completedAtUtc)
    {
        if (Status is not (ClientTodoStatus.Open or ClientTodoStatus.InProgress))
            throw new InvalidOperationException("Only an open or in-progress todo can be completed.");
        if (completedByUserId == Guid.Empty) throw new ArgumentException("A completing user is required.", nameof(completedByUserId));
        var utc = EnsureUtc(completedAtUtc, nameof(completedAtUtc));
        Status = ClientTodoStatus.Completed;
        CompletedByUserId = completedByUserId;
        CompletedAtUtc = utc;
        UpdatedAtUtc = utc;
    }

    public void Reopen(DateTimeOffset updatedAtUtc)
    {
        if (Status != ClientTodoStatus.Completed) throw new InvalidOperationException("Only a completed todo can be reopened.");
        Status = ClientTodoStatus.Open;
        CompletedByUserId = null;
        CompletedAtUtc = null;
        Touch(updatedAtUtc);
    }

    public void Cancel(DateTimeOffset updatedAtUtc)
    {
        if (Status is ClientTodoStatus.Completed or ClientTodoStatus.Cancelled)
            throw new InvalidOperationException("A completed or cancelled todo cannot be cancelled.");
        Status = ClientTodoStatus.Cancelled;
        Touch(updatedAtUtc);
    }

    public bool IsOverdue(DateOnly today) =>
        Status is ClientTodoStatus.Open or ClientTodoStatus.InProgress
        && DueDate.HasValue
        && DueDate.Value < today;

    private void SetDetails(string title, string? description, Guid assignedToUserId,
        DateOnly? dueDate, ClientTodoPriority priority)
    {
        if (assignedToUserId == Guid.Empty) throw new ArgumentException("An assignee is required.", nameof(assignedToUserId));
        if (!Enum.IsDefined(priority)) throw new ArgumentOutOfRangeException(nameof(priority));
        Title = NormalizeRequired(title, MaximumTitleLength, nameof(title));
        Description = Normalize(description, MaximumDescriptionLength, nameof(description));
        AssignedToUserId = assignedToUserId;
        DueDate = dueDate;
        Priority = priority;
    }

    private void EnsureEditable()
    {
        if (Status is ClientTodoStatus.Completed or ClientTodoStatus.Cancelled)
            throw new InvalidOperationException("A completed or cancelled todo cannot be edited.");
    }

    private void Touch(DateTimeOffset value) => UpdatedAtUtc = EnsureUtc(value, nameof(value));
    private static DateTimeOffset EnsureUtc(DateTimeOffset value, string name) =>
        value.Offset == TimeSpan.Zero ? value : throw new ArgumentException("The timestamp must be UTC.", name);
    private static string NormalizeRequired(string value, int max, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Normalize(value, max, name);
    }
    private static string Normalize(string? value, int max, string name)
    {
        var result = (value ?? string.Empty).Trim();
        return result.Length <= max ? result : throw new ArgumentOutOfRangeException(name, $"Value cannot exceed {max} characters.");
    }
}
