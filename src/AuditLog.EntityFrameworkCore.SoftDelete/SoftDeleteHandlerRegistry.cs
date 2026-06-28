namespace AuditLog.EntityFrameworkCore.SoftDelete;

public sealed class SoftDeleteHandlerRegistry
{
    private readonly Dictionary<Type, object> _handlers = new();

    public void Register<TEntity>(ISoftDeleteHandler<TEntity> handler)
        where TEntity : class, ISoftDeleteEntity
    {
        _handlers[typeof(TEntity)] = handler;
    }

    public void Register(Type entityType, object handler)
    {
        _handlers[entityType] = handler;
    }

    public bool TryGetHandler(Type entityType, out object? handler)
        => _handlers.TryGetValue(entityType, out handler);
}
