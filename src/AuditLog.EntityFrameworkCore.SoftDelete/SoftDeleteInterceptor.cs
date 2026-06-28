using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AuditLog.EntityFrameworkCore.SoftDelete;

public sealed class SoftDeleteInterceptor : SaveChangesInterceptor
{
    private readonly SoftDeleteHandlerRegistry? _registry;
    private readonly Func<DateTime> _timestampFactory;

    public SoftDeleteInterceptor(
        SoftDeleteHandlerRegistry? registry = null,
        Func<DateTime>? timestampFactory = null)
    {
        _registry = registry;
        _timestampFactory = timestampFactory ?? (() => DateTime.UtcNow);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ProcessEntriesSync(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await ProcessEntriesAsync(eventData.Context);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ProcessEntriesSync(DbContext? context)
    {
        if (context is null) return;
        context.ChangeTracker.DetectChanges();
        var now = _timestampFactory();

        foreach (var entry in context.ChangeTracker.Entries<ISoftDeleteEntity>().Where(e => e.State == EntityState.Deleted).ToList())
        {
            MarkSoftDeleted(entry, now);

            var entityType = entry.Entity.GetType();
            if (_registry is not null && _registry.TryGetHandler(entityType, out var handlerObj) && handlerObj is not null)
            {
                var handleMethod = handlerObj.GetType().GetMethod("HandleDeleteAsync")!;
                var task = (Task)handleMethod.Invoke(handlerObj, [context, entry.Entity, now])!;
                task.GetAwaiter().GetResult();
            }
            else
            {
                SoftDeleteCascadeHandler.HandleDeleteAsync(context, entry, now).GetAwaiter().GetResult();
            }
        }
    }

    private async Task ProcessEntriesAsync(DbContext? context)
    {
        if (context is null) return;
        context.ChangeTracker.DetectChanges();
        var now = _timestampFactory();

        foreach (var entry in context.ChangeTracker.Entries<ISoftDeleteEntity>().Where(e => e.State == EntityState.Deleted).ToList())
        {
            MarkSoftDeleted(entry, now);

            var entityType = entry.Entity.GetType();
            if (_registry is not null && _registry.TryGetHandler(entityType, out var handlerObj) && handlerObj is not null)
            {
                var handleMethod = handlerObj.GetType().GetMethod("HandleDeleteAsync")!;
                var task = (Task)handleMethod.Invoke(handlerObj, [context, entry.Entity, now])!;
                await task;
            }
            else
            {
                await SoftDeleteCascadeHandler.HandleDeleteAsync(context, entry, now);
            }
        }
    }

    public static void MarkSoftDeleted(EntityEntry entry, DateTime deletedAt)
    {
        if (entry.State == EntityState.Modified && (bool)entry.Property("IsDeleted").CurrentValue!)
            return;

        if (entry.State == EntityState.Deleted)
            entry.State = EntityState.Unchanged;

        entry.Property("IsDeleted").CurrentValue = true;
        entry.Property("IsDeleted").IsModified = true;

        entry.Property("DeletedAt").CurrentValue = deletedAt;
        entry.Property("DeletedAt").IsModified = true;

        BlindOwnedEntities(entry);
    }

    public static void BlindOwnedEntities(EntityEntry currentEntry)
    {
        foreach (var navigation in currentEntry.Navigations)
        {
            if (navigation.Metadata is not INavigation navMetadata || !navMetadata.TargetEntityType.IsOwned())
                continue;

            if (navigation.CurrentValue == null)
                continue;

            var ownedEntry = currentEntry.Context.Entry(navigation.CurrentValue);
            ownedEntry.State = EntityState.Unchanged;

            BlindOwnedEntities(ownedEntry);
        }
    }
}
