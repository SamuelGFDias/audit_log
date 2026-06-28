using AuditLog.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AuditLog.EntityFrameworkCore;

public sealed class AuditSaveInterceptor : SaveChangesInterceptor
{
    private readonly AuditRegistry _registry;
    private readonly Func<AuditExecutionContext> _contextFactory;

    public AuditSaveInterceptor(
        AuditRegistry registry,
        Func<AuditExecutionContext>? contextFactory = null)
    {
        _registry = registry;
        _contextFactory = contextFactory ?? (() => new AuditExecutionContext(
            DateTimeOffset.UtcNow, null, null));
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

        var ctx = _contextFactory();
        var addMethod = typeof(DbContext).GetMethod(nameof(DbContext.Add), 1, [typeof(object)])!;

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            var entityType = entry.Entity.GetType();

            foreach (var (_, auditLogType, descriptorObj) in _registry.GetAll())
            {
                var descriptorType = typeof(IAuditDescriptor<,>)
                    .MakeGenericType(entityType, auditLogType);

                if (!descriptorType.IsInstanceOfType(descriptorObj))
                    continue;

                var createLogMethod = descriptorType.GetMethod("CreateLog")!;
                var createChildLogsMethod = descriptorType.GetMethod("CreateChildLogs")!;

                var typedEntry = GetGenericEntry(context, entry, entityType);

                var auditLog = createLogMethod.Invoke(descriptorObj, [typedEntry, ctx]);
                if (auditLog is null) continue;

                context.Add(auditLog);

                var childLogs = (System.Collections.IEnumerable?)
                    createChildLogsMethod.Invoke(descriptorObj, [typedEntry, ctx]);

                if (childLogs is not null)
                {
                    foreach (var child in childLogs)
                    {
                        if (child is not null)
                            context.Add(child);
                    }
                }
            }
        }
    }

    private static object GetGenericEntry(DbContext context, EntityEntry entry, Type entityType)
    {
        var entryMethod = typeof(DbContext)
            .GetMethods()
            .First(m => m.Name == nameof(DbContext.Entry) && m.IsGenericMethod && m.GetParameters().Length == 1)
            .MakeGenericMethod(entityType);

        return entryMethod.Invoke(context, [entry.Entity])!;
    }
}
