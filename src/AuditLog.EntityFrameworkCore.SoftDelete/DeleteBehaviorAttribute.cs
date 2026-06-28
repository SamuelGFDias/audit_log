namespace AuditLog.EntityFrameworkCore.SoftDelete;

public enum CascadeDeleteBehavior
{
    Cascade,
    Restrict,
    SetNull
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class DeleteBehaviorAttribute : Attribute
{
    public CascadeDeleteBehavior Behavior { get; }
    public DeleteBehaviorAttribute(CascadeDeleteBehavior behavior) => Behavior = behavior;
}
