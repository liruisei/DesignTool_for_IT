namespace WinDesign.Contracts.Pbs;

public sealed record PbsNodeDto(
    Guid Id,
    Guid ProjectId,
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
