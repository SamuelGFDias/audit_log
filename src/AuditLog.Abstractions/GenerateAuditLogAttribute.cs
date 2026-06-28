namespace AuditLog.Abstractions;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GenerateAuditLogAttribute : Attribute
{
}
