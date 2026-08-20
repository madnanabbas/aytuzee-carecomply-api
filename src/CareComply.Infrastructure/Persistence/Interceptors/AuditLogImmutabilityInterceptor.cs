using CareComply.Infrastructure.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CareComply.Infrastructure.Persistence.Interceptors;

internal sealed class AuditLogImmutabilityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        RejectMutations(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        RejectMutations(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void RejectMutations(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var mutationExists = context.ChangeTracker.Entries<AuditLog>().Any(entry =>
            entry.State is EntityState.Modified or EntityState.Deleted);

        if (mutationExists)
        {
            throw new InvalidOperationException(
                "Audit records are append-only and cannot be modified or deleted.");
        }
    }
}
