using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace AuditLog.Abstractions;

public interface IAuditDescriptor<TEntity, TAuditLog>
    where TEntity : class
    where TAuditLog : class
{
    TAuditLog CreateLog(EntityEntry<TEntity> entry, AuditExecutionContext context);

    IReadOnlyList<TAuditLog> CreateChildLogs(
        EntityEntry<TEntity> entry,
        AuditExecutionContext context);
}
