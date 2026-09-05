using System.Security.Cryptography;
using CareComply.Application.Abstractions.Clients;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Clients;

internal sealed class EfClientTodoStore(CareComplyDbContext context) : IClientTodoStore
{
    public async Task<IReadOnlyList<ClientTodoAssignee>> ListActiveAssigneesAsync(CancellationToken cancellationToken) =>
        await context.Users.AsNoTracking().Where(x => x.IsActive && x.IsEmployee)
            .OrderBy(x => x.LastName).ThenBy(x => x.FirstName)
            .Select(x => new ClientTodoAssignee(x.Id, (x.FirstName + " " + x.LastName).Trim()))
            .ToArrayAsync(cancellationToken);

    public async Task<ClientTodoList?> ListForClientAsync(Guid clientId, DateOnly today, CancellationToken cancellationToken)
    {
        if (!await context.Clients.AsNoTracking().AnyAsync(x => x.Id == clientId, cancellationToken)) return null;
        var rows = await (from todo in context.ClientTodos.AsNoTracking()
                          where todo.ClientId == clientId
                          join assigned in context.Users.AsNoTracking() on todo.AssignedToUserId equals assigned.Id
                          join created in context.Users.AsNoTracking() on todo.CreatedByUserId equals created.Id
                          orderby todo.Status, todo.DueDate, todo.CreatedAtUtc descending
                          select new TodoRow(todo, assigned.FirstName + " " + assigned.LastName,
                              created.FirstName + " " + created.LastName)).ToArrayAsync(cancellationToken);
        return new(rows.Select(x => Map(x.Todo, x.AssignedName, x.CreatedName, today)).ToArray());
    }

    public async Task<ClientTodoMutationResult> CreateAsync(ClientTodoCreateRequest request, CancellationToken cancellationToken)
    {
        if (!await context.Clients.AnyAsync(x => x.Id == request.ClientId, cancellationToken)) return new(ClientTodoMutationStatus.ClientNotFound);
        if (!await ActiveUser(request.AssignedToUserId, cancellationToken)) return new(ClientTodoMutationStatus.InvalidAssignee);
        var todo = ClientTodo.Create(Guid.CreateVersion7(), request.ClientId, request.Title, request.Description,
            request.AssignedToUserId, request.DueDate, request.Priority, request.ActorUserId, request.NowUtc);
        context.ClientTodos.Add(todo);
        await context.SaveChangesAsync(cancellationToken);
        return new(ClientTodoMutationStatus.Succeeded, await MapWithNames(todo, DateOnly.FromDateTime(request.NowUtc.UtcDateTime), cancellationToken));
    }

    public async Task<ClientTodoMutationResult> UpdateAsync(ClientTodoUpdateRequest request, CancellationToken cancellationToken)
    {
        var todo = await context.ClientTodos.SingleOrDefaultAsync(x => x.Id == request.TodoId && x.ClientId == request.ClientId, cancellationToken);
        if (todo is null) return await Missing(request.ClientId, cancellationToken);
        if (!VersionMatches(todo.RowVersion, request.EditorVersion)) return new(ClientTodoMutationStatus.Conflict);
        if (!await ActiveUser(request.AssignedToUserId, cancellationToken)) return new(ClientTodoMutationStatus.InvalidAssignee);
        try { todo.UpdateDetails(request.Title, request.Description, request.AssignedToUserId, request.DueDate, request.Priority, request.NowUtc); }
        catch (InvalidOperationException) { return new(ClientTodoMutationStatus.InvalidState); }
        return await Save(todo, request.Today, cancellationToken);
    }

    public async Task<ClientTodoMutationResult> TransitionAsync(ClientTodoTransitionStoreRequest request, CancellationToken cancellationToken)
    {
        var todo = await context.ClientTodos.SingleOrDefaultAsync(x => x.Id == request.TodoId && x.ClientId == request.ClientId, cancellationToken);
        if (todo is null) return await Missing(request.ClientId, cancellationToken);
        if (!VersionMatches(todo.RowVersion, request.EditorVersion)) return new(ClientTodoMutationStatus.Conflict);
        try
        {
            switch (request.Transition)
            {
                case ClientTodoTransition.Complete: todo.Complete(request.ActorUserId, request.NowUtc); break;
                case ClientTodoTransition.Reopen: todo.Reopen(request.NowUtc); break;
                case ClientTodoTransition.Cancel: todo.Cancel(request.NowUtc); break;
                default: throw new ArgumentOutOfRangeException(nameof(request));
            }
        }
        catch (InvalidOperationException) { return new(ClientTodoMutationStatus.InvalidState); }
        return await Save(todo, request.Today, cancellationToken);
    }

    private async Task<ClientTodoMutationResult> Save(ClientTodo todo, DateOnly today, CancellationToken ct)
    {
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return new(ClientTodoMutationStatus.Conflict); }
        return new(ClientTodoMutationStatus.Succeeded, await MapWithNames(todo, today, ct));
    }
    private async Task<ClientTodoMutationResult> Missing(Guid clientId, CancellationToken ct) =>
        await context.Clients.AsNoTracking().AnyAsync(x => x.Id == clientId, ct)
            ? new(ClientTodoMutationStatus.TodoNotFound) : new(ClientTodoMutationStatus.ClientNotFound);
    private Task<bool> ActiveUser(Guid id, CancellationToken ct) => context.Users.AsNoTracking()
        .AnyAsync(x => x.Id == id && x.IsActive && x.IsEmployee, ct);
    private async Task<ClientTodoItem> MapWithNames(ClientTodo todo, DateOnly today, CancellationToken ct)
    {
        var names = await context.Users.AsNoTracking().Where(x => x.Id == todo.AssignedToUserId || x.Id == todo.CreatedByUserId)
            .ToDictionaryAsync(x => x.Id, x => (x.FirstName + " " + x.LastName).Trim(), ct);
        return Map(todo, names.GetValueOrDefault(todo.AssignedToUserId, "Unknown user"), names.GetValueOrDefault(todo.CreatedByUserId, "Unknown user"), today);
    }
    private static ClientTodoItem Map(ClientTodo x, string assigned, string created, DateOnly today) => new(x.Id, x.ClientId,
        x.Title, x.Description, x.AssignedToUserId, assigned.Trim(), x.DueDate, x.Priority, x.Status,
        x.IsOverdue(today),
        x.CreatedByUserId, created.Trim(), x.CreatedAtUtc, x.UpdatedAtUtc, x.CompletedByUserId, x.CompletedAtUtc, CreateVersion(x.RowVersion));
    private static string CreateVersion(byte[] value) => Convert.ToHexString(SHA256.HashData(value));
    private static bool VersionMatches(byte[] value, string supplied)
    {
        var expected = System.Text.Encoding.ASCII.GetBytes(CreateVersion(value));
        var actual = System.Text.Encoding.ASCII.GetBytes(supplied.ToUpperInvariant());
        return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
    }
    private sealed record TodoRow(ClientTodo Todo, string AssignedName, string CreatedName);
}
