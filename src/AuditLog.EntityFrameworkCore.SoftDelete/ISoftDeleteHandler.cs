using Microsoft.EntityFrameworkCore;

namespace AuditLog.EntityFrameworkCore.SoftDelete;

public interface ISoftDeleteHandler<TEntity>
    where TEntity : class, ISoftDeleteEntity
{
    Task HandleDeleteAsync(DbContext context, TEntity entity, DateTime deletedAt);
}
