namespace WinDesign.Contracts.Pbs;

public sealed record PbsNodeDto(
    Guid Id,
    Guid PlantId,
    Guid? ParentId,
    string Code,
    string Name,
    string NodeType,
    int SortOrder,
    string? Description,
    int ChildrenCount,
    int EquipmentCount);

public sealed record CreatePbsNodeRequest(
    Guid? ParentId,
    string Code,
    string Name,
    string NodeType,
    int SortOrder,
    string? Description);

public sealed record UpdatePbsNodeRequest(
    Guid? ParentId,
    string Code,
    string Name,
    string NodeType,
    int SortOrder,
    string? Description);

/// <summary>
/// 设计端在装置下新增分类节点（回路 / 建筑物）。
/// 父节点由路由中的装置决定，类型只允许 Loop 与 Building。
/// </summary>
public sealed record CreateUnitChildRequest(
    string Code,
    string Name,
    string NodeType,
    string? Description);

/// <summary>设计端编辑装置下的回路 / 建筑物节点。</summary>
public sealed record UpdateUnitChildRequest(
    string Code,
    string Name,
    string? Description);
