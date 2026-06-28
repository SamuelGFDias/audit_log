using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AuditLog.EntityFrameworkCore.SoftDelete;

public sealed class SoftDeleteInterceptor : SaveChangesInterceptor
{
    private readonly Func<DateTime> _timestampFactory;

    public SoftDeleteInterceptor(Func<DateTime>? timestampFactory = null)
    {
        _timestampFactory = timestampFactory ?? (() => DateTime.UtcNow);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ProcessEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ProcessEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ProcessEntries(DbContext? context)
    {
        if (context is null) return;

        context.ChangeTracker.DetectChanges();

        var now = _timestampFactory();

        var deletedEntries = context.ChangeTracker
            .Entries<ISoftDeleteEntity>()
            .Where(e => e.State == EntityState.Deleted)
            .ToList();

        foreach (var entry in deletedEntries)
        {
            SoftDeleteCascadeHandler.HandleDeleteAsync(context, entry, now).GetAwaiter().GetResult();
        }
    }
}
