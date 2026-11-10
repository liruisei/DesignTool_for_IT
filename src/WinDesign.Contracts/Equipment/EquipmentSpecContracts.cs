namespace WinDesign.Contracts.Equipment;

/// <summary>规格书参数行。存储层 ContentJson 为 JSON 对象，接口边界改用有序键值对。</summary>
public sealed record SpecParameterDto
{
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

/// <summary>新建/编辑规格书。设备归属只在新建接口的路径中指定，编辑时不可变更。</summary>
public record EquipmentSpecUpsertRequest
{
    public string SpecCode { get; init; } = string.Empty;
    public string SpecName { get; init; } = string.Empty;
    public IReadOnlyList<SpecParameterDto>? Parameters { get; init; }
}

public sealed record EquipmentSpecDto : EquipmentSpecUpsertRequest
{
    public Guid Id { get; init; }
    public Guid EquipmentId { get; init; }
    public string EquipmentTagNo { get; init; } = string.Empty;
    public string EquipmentName { get; init; } = string.Empty;
    public int Version { get; init; }
    /// <summary>旧 ContentJson 无法完整解析时的提示；正常数据为 null。</summary>
    public string? ContentWarning { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
