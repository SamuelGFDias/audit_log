namespace AuditLog.EntityFrameworkCore.SoftDelete;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class GenerateSoftDeleteAttribute : Attribute
{
}
