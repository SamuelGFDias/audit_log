using System.Linq.Expressions;

namespace AuditLog.Abstractions;

public abstract class AuditConfigurator<TEntity>
    where TEntity : class
{
    protected AuditPropertyBuilder<TEntity, TProperty> For<TProperty>(
        Expression<Func<TEntity, TProperty>> expression)
    {
        return default!;
    }

    protected AuditOwnedBuilder<TEntity, TOwned> ForOwned<TOwned>(
        Expression<Func<TEntity, TOwned>> expression)
        where TOwned : class
    {
        return default!;
    }

    protected void ForOwned<TOwned>(
        Expression<Func<TEntity, TOwned>> expression,
        Action<AuditOwnedBuilder<TEntity, TOwned>> configure)
        where TOwned : class
    {
    }

    protected AuditCollectionBuilder<TEntity, TElement> ForEach<TElement>(
        Expression<Func<TEntity, IEnumerable<TElement>>> expression)
        where TElement : class
    {
        return default!;
    }
}
