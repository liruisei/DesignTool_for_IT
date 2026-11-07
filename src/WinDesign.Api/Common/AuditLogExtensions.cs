using WinDesign.Domain.Entities;
using WinDesign.Infrastructure;

namespace WinDesign.Api.Common;

public static class AuditLogExtensions
{
    public static void AddAudit(
        this WinDesignDbContext db,
        Guid? userId,
        string userName,
        string action,
        string entityType,
        string? entityId,
        string? detail = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            UserName = userName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Detail = detail
        });
    }
}
