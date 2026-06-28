using System.Linq.Expressions;

namespace AuditLog.Abstractions;

public sealed class AuditOwnedBuilder<TEntity, TOwned>
    where TOwned : class
{
    public AuditOwnedBuilder<TEntity, TOwned> Key()
    {
        return this;
    }

    public AuditOwnedBuilder<TEntity, TOwned> Ignore()
    {
        return this;
    }

    public AuditOwnedBuilder<TEntity, TOwned> Sensitive()
    {
        return this;
    }

    public AuditOwnedBuilder<TEntity, TOwned> AlwaysAudit()
    {
        return this;
    }

    public AuditOwnedBuilder<TEntity, TOwned> WithColumnName(string columnName)
    {
        return this;
    }

    public AuditPropertyBuilder<TEntity, TProperty> For<TProperty>(
        Expression<Func<TOwned, TProperty>> expression)
    {
        return default!;
    }
}
