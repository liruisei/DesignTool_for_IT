namespace WinDesign.Contracts.Materials;

/// <summary>材料/附件按项目维护，可选关联当前项目范围内的设备。</summary>
public record MaterialItemUpsertRequest
{
    public Guid? EquipmentId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Spec { get; init; }
    public string? Material { get; init; }
    public string Unit { get; init; } = "个";
    public decimal Quantity { get; init; }
    public string? Remark { get; init; }
}

public sealed record MaterialItemDto : MaterialItemUpsertRequest
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string? EquipmentTagNo { get; init; }
    public string? EquipmentName { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
