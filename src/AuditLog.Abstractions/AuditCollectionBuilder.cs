using System.Linq.Expressions;

namespace AuditLog.Abstractions;

public sealed class AuditCollectionBuilder<TEntity, TElement>
    where TElement : class
{
    public AuditCollectionBuilder<TEntity, TElement> ParentKey(
        Expression<Func<TElement, object>> expression)
    {
        return this;
    }

    public AuditCollectionBuilder<TEntity, TElement> Key(
        Expression<Func<TElement, object>> expression)
    {
        return this;
    }

    public AuditCollectionBuilder<TEntity, TElement> AuditLogName(string name)
    {
        return this;
    }

    public AuditCollectionBuilder<TEntity, TElement> Configure(
        Action<AuditCollectionItemConfigurator<TElement>> configure)
    {
        return this;
    }
}

public sealed class AuditCollectionItemConfigurator<TElement>
    where TElement : class
{
    public AuditPropertyBuilder<TElement, TProperty> For<TProperty>(
        Expression<Func<TElement, TProperty>> expression)
    {
        return default!;
    }
}
