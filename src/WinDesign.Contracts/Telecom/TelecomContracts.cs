namespace WinDesign.Contracts.Telecom;

/// <summary>设备样本：专业 + 设备类型决定归类，设计端按端口数与可用率计算所需台数。</summary>
public sealed record SwitchModelDto(
    Guid Id,
    string Code,
    string Name,
    string? Discipline,
    string? TypeCode,
    string? TypeName,
    int ElectricalPorts,
    int? UplinkPorts,
    bool IsActive,
    int SortOrder,
    string? Remark)
{
    /// <summary>按设备类型名称判断是否为 PoE 交换机；仅用于设计端默认选型。</summary>
    public bool IsPoe => TypeName?.Contains("PoE", StringComparison.OrdinalIgnoreCase) == true;
}

public sealed record SwitchModelUpsertRequest(
    string Code,
    string Name,
    string? Discipline,
    string? TypeCode,
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
