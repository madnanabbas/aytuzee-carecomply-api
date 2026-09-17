using System.Security.Cryptography;
using CareComply.Application.Abstractions.Clients;
using CareComply.Domain.Clients;
using CareComply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareComply.Infrastructure.Clients;

internal sealed class EfClientCaregiverStore(CareComplyDbContext context, TimeProvider timeProvider) : IClientCaregiverStore
{
    private DateOnly Today => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
    public async Task<ClientCaregiverList?> ListAsync(Guid clientId, CancellationToken ct)
    {
        if (!await context.Clients.AsNoTracking().AnyAsync(x => x.Id == clientId, ct)) return null;
        var assignments = await context.ClientCaregivers.AsNoTracking().Where(x => x.ClientId == clientId)
            .OrderBy(x => x.Status).ThenBy(x => x.Type).ThenBy(x => x.StartDate).ToArrayAsync(ct);
        return new(await MapMany(assignments, ct));
    }

    public async Task<IReadOnlyList<ClientCaregiverCandidate>> CandidatesAsync(CancellationToken ct)
    {
        var users = await context.Users.AsNoTracking().Where(x => x.IsActive && x.IsEmployee)
            .OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ToArrayAsync(ct);
        return users.Select(x => new ClientCaregiverCandidate(x.Id, Name(x.FirstName, x.LastName),
            Title(x), x.PhoneNumber ?? string.Empty, x.Email ?? string.Empty)).ToArray();
    }

    public async Task<ClientCaregiverMutation> CreateAsync(ClientCaregiverCreate request, CancellationToken ct)
    {
        if (!await context.Clients.AnyAsync(x => x.Id == request.ClientId, ct)) return new(ClientCaregiverMutationStatus.ClientNotFound);
        if (!await context.Users.AnyAsync(x => x.Id == request.UserId && x.IsActive && x.IsEmployee, ct)) return new(ClientCaregiverMutationStatus.InvalidCaregiver);
        if (await context.ClientCaregivers.AnyAsync(x => x.ClientId == request.ClientId && x.UserId == request.UserId && x.Status == ClientCaregiverStatus.Active && (!x.EndDate.HasValue || x.EndDate >= Today), ct))
            return new(ClientCaregiverMutationStatus.DuplicateActiveAssignment);
        if (request.Type == ClientCaregiverType.Primary && !request.EndDate.HasValue && await HasOtherPrimary(request.ClientId, null, ct))
            return new(ClientCaregiverMutationStatus.PrimaryConflict);
        var entity = ClientCaregiver.Create(Guid.CreateVersion7(), request.ClientId, request.UserId, request.Type,
            request.IsPreferred, request.Services, request.StartDate, request.EndDate, request.Notes, request.ActorUserId, request.NowUtc, request.RelationshipToClient, request.LivesWithClient);
        context.ClientCaregivers.Add(entity);
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return new(ClientCaregiverMutationStatus.Conflict); }
        return new(ClientCaregiverMutationStatus.Succeeded, await Map(entity, ct));
    }

    public async Task<ClientCaregiverMutation> UpdateAsync(ClientCaregiverUpdate request, CancellationToken ct)
    {
        var entity = await context.ClientCaregivers.SingleOrDefaultAsync(x => x.Id == request.AssignmentId && x.ClientId == request.ClientId, ct);
        if (entity is null) return await Missing(request.ClientId, ct);
        if (!VersionMatches(entity.RowVersion, request.EditorVersion)) return new(ClientCaregiverMutationStatus.Conflict);
        if (request.Type == ClientCaregiverType.Primary && !request.EndDate.HasValue && await HasOtherPrimary(request.ClientId, entity.Id, ct))
            return new(ClientCaregiverMutationStatus.PrimaryConflict);
        try { entity.Update(request.Type, request.IsPreferred, request.Services, request.StartDate, request.EndDate, request.Notes, request.NowUtc, request.RelationshipToClient, request.LivesWithClient); }
        catch (InvalidOperationException) { return new(ClientCaregiverMutationStatus.InvalidState); }
        return await Save(entity, ct);
    }

    public async Task<ClientCaregiverMutation> EndAsync(ClientCaregiverEnd request, CancellationToken ct)
    {
        var entity = await context.ClientCaregivers.SingleOrDefaultAsync(x => x.Id == request.AssignmentId && x.ClientId == request.ClientId, ct);
        if (entity is null) return await Missing(request.ClientId, ct);
        if (!VersionMatches(entity.RowVersion, request.EditorVersion)) return new(ClientCaregiverMutationStatus.Conflict);
        try { entity.End(request.EndDate, request.NowUtc); }
        catch (InvalidOperationException) { return new(ClientCaregiverMutationStatus.InvalidState); }
        return await Save(entity, ct);
    }

    private Task<bool> HasOtherPrimary(Guid clientId, Guid? exceptId, CancellationToken ct) => context.ClientCaregivers.AsNoTracking()
        .AnyAsync(x => x.ClientId == clientId && x.Id != exceptId && x.Type == ClientCaregiverType.Primary && x.Status == ClientCaregiverStatus.Active && (!x.EndDate.HasValue || x.EndDate >= Today), ct);
    private async Task<ClientCaregiverMutation> Save(ClientCaregiver entity, CancellationToken ct)
    { try { await context.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { return new(ClientCaregiverMutationStatus.Conflict); } catch (DbUpdateException) { return new(ClientCaregiverMutationStatus.Conflict); } return new(ClientCaregiverMutationStatus.Succeeded, await Map(entity, ct)); }
    private async Task<ClientCaregiverMutation> Missing(Guid clientId, CancellationToken ct) => await context.Clients.AsNoTracking().AnyAsync(x => x.Id == clientId, ct)
        ? new(ClientCaregiverMutationStatus.AssignmentNotFound) : new(ClientCaregiverMutationStatus.ClientNotFound);
    private async Task<ClientCaregiverItem[]> MapMany(ClientCaregiver[] values, CancellationToken ct)
    { var users = await context.Users.AsNoTracking().Where(x => values.Select(a => a.UserId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct); return values.Select(x => Map(x, users.GetValueOrDefault(x.UserId), Today)).ToArray(); }
    private async Task<ClientCaregiverItem> Map(ClientCaregiver value, CancellationToken ct)
    { var user = await context.Users.AsNoTracking().SingleAsync(x => x.Id == value.UserId, ct); return Map(value, user, Today); }
    private static ClientCaregiverItem Map(ClientCaregiver x, Identity.ApplicationUser? user, DateOnly today) => new(x.Id, x.UserId,
        user is null ? "Unknown user" : Name(user.FirstName, user.LastName), user is null ? "Caregiver" : Title(user), user?.PhoneNumber ?? string.Empty, user?.Email ?? string.Empty,
        x.Type, x.IsPreferred, x.Services, x.StartDate, x.EndDate, x.Notes,
        x.Status == ClientCaregiverStatus.Ended || x.EndDate < today ? ClientCaregiverStatus.Ended : ClientCaregiverStatus.Active,
        Version(x.RowVersion), x.RelationshipToClient, x.LivesWithClient);
    private static string Title(Identity.ApplicationUser user) => string.IsNullOrWhiteSpace(user.JobTitle) ? "Caregiver" : user.JobTitle;
    private static string Name(string first, string last) => (first + " " + last).Trim();
    private static string Version(byte[] value) => Convert.ToHexString(SHA256.HashData(value));
    private static bool VersionMatches(byte[] value, string supplied) { var a = System.Text.Encoding.ASCII.GetBytes(Version(value)); var b = System.Text.Encoding.ASCII.GetBytes(supplied.ToUpperInvariant()); return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b); }
}
