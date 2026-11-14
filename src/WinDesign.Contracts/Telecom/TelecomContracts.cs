namespace WinDesign.Contracts.Telecom;

/// <summary>交换机样本：设计端按端口数与可用率计算所需台数。</summary>
public sealed record SwitchModelDto(
    Guid Id,
    string Code,
    string Name,
    string SwitchType,
    int ElectricalPorts,
    int? UplinkPorts,
    bool IsActive,
    int SortOrder,
    string? Remark);

public sealed record SwitchModelUpsertRequest(
    string Code,
    string Name,
    string SwitchType,
    int ElectricalPorts,
    int? UplinkPorts,
    bool IsActive,
    int SortOrder,
    string? Remark);

/// <summary>
/// 电信房间统计行。建筑物信息来自关联的 PBS 建筑物节点，便于按建筑/楼层汇总。
/// </summary>
public sealed record TelecomRoomDto(
    Guid Id,
    Guid ProjectId,
    Guid PbsNodeId,
    string BuildingCode,
    string BuildingName,
    string Floor,
    string Code,
    string Name,
    int DataPointCount,
    int PhonePointCount,
    int ApCount,
    string? Remark,
    int SortOrder);

public sealed record TelecomRoomUpsertRequest(
    Guid PbsNodeId,
    string Floor,
    string Code,
    string Name,
    int DataPointCount,
    int PhonePointCount,
    int ApCount,
    string? Remark,
    int SortOrder);
