namespace AuditLog.EntityFrameworkCore.SoftDelete;

public interface ISoftDeleteEntity
{
    DateTime? DeletedAt { get; set; }
    bool IsDeleted { get; set; }
}
