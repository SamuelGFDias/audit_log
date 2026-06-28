namespace AuditLog.EntityFrameworkCore.SoftDelete;

public class RestrictDeleteViolationException(
    string entityName,
    object[] primaryKeyValues)
    : InvalidOperationException(
        $"Cannot delete {entityName} with PK ({string.Join(", ", primaryKeyValues)}) " +
        "because it has dependent entities with Restrict or NoAction delete behavior.")
{
    public string EntityName { get; } = entityName;
    public object[] PrimaryKeyValues { get; } = primaryKeyValues;
}
