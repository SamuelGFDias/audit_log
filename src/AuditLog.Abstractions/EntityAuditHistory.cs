namespace AuditLog.Abstractions;

public sealed class EntityAuditHistory<TEntity, TAuditLog>
    where TEntity : class
    where TAuditLog : class
{
    public TEntity Entity { get; init; } = null!;

    public IReadOnlyList<TAuditLog> Logs { get; init; } = [];
}
