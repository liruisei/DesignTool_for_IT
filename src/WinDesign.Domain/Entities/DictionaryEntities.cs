using WinDesign.Domain.Common;

namespace WinDesign.Domain.Entities;

public class DictionaryType : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public int SortOrder { get; set; }

    public ICollection<DictionaryItem> Items { get; set; } = new List<DictionaryItem>();
}

public class DictionaryItem : EntityBase
{
    public Guid TypeId { get; set; }
    public DictionaryType Type { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ExtraJson { get; set; }
}

public class AuditLog : EntityBase
{
    public Guid? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Detail { get; set; }
}
