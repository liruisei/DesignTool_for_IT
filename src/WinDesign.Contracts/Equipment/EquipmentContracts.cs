namespace WinDesign.Contracts.Equipment;

public sealed record EquipmentDto(
    Guid Id,
    Guid ProjectId,
    Guid? PbsNodeId,
    string? PbsNodeName,
    string TagNo,
    string Name,
    string? TypeCode,
    string Discipline,
    string Status,
    string? Location,
    string? Description,
    DateTime CreatedAt);

public sealed record CreateEquipmentRequest(
    Guid? PbsNodeId,
    string TagNo,
    string Name,
    string? TypeCode,
    string Discipline,
    string? Status,
    string? Location,
    string? Description);

public sealed record UpdateEquipmentRequest(
    Guid? PbsNodeId,
    string TagNo,
    string Name,
    string? TypeCode,
    string Discipline,
    string? Status,
    string? Location,
    string? Description);
