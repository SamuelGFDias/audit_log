namespace AuditLog.Abstractions;

public sealed class AuditPropertyBuilder<TEntity, TProperty>
{
    public AuditPropertyBuilder<TEntity, TProperty> Key()
    {
        return this;
    }

    public AuditPropertyBuilder<TEntity, TProperty> Ignore()
    {
        return this;
    }

    public AuditPropertyBuilder<TEntity, TProperty> Sensitive()
    {
        return this;
    }

    public AuditPropertyBuilder<TEntity, TProperty> AlwaysAudit()
    {
        return this;
    }

    public AuditPropertyBuilder<TEntity, TProperty> WithColumnName(string columnName)
    {
        return this;
    }

    public AuditPropertyBuilder<TEntity, TProperty> HasMaxLength(int maxLength)
    {
        return this;
    }

    public AuditPropertyBuilder<TEntity, TProperty> IsRequired()
    {
        return this;
    }
}
