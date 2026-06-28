namespace AuditLog.Abstractions;

public sealed class AuditRegistry
{
    private readonly Dictionary<Type, Dictionary<Type, object>> _descriptors = new();

    public AuditRegistry Add<TEntity, TAuditLog>(IAuditDescriptor<TEntity, TAuditLog> descriptor)
        where TEntity : class
        where TAuditLog : class
    {
        if (!_descriptors.TryGetValue(typeof(TEntity), out var logMap))
        {
            logMap = new Dictionary<Type, object>();
            _descriptors[typeof(TEntity)] = logMap;
        }

        logMap[typeof(TAuditLog)] = descriptor;
        return this;
    }

    public bool TryGetDescriptor<TEntity, TAuditLog>(out IAuditDescriptor<TEntity, TAuditLog>? descriptor)
        where TEntity : class
        where TAuditLog : class
    {
        descriptor = null;

        if (_descriptors.TryGetValue(typeof(TEntity), out var logMap) &&
            logMap.TryGetValue(typeof(TAuditLog), out var obj))
        {
            descriptor = (IAuditDescriptor<TEntity, TAuditLog>)obj;
            return true;
        }

        return false;
    }

    public IReadOnlyCollection<(Type Entity, Type AuditLog, object Descriptor)> GetAll()
    {
        var results = new List<(Type, Type, object)>();

        foreach (var (entityType, logMap) in _descriptors)
        {
            foreach (var (auditLogType, descriptor) in logMap)
            {
                results.Add((entityType, auditLogType, descriptor));
            }
        }

        return results;
    }
}
