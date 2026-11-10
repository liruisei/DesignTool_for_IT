namespace WinDesign.Contracts.Equipment;

public record EquipmentUpsertRequest
{
    public Guid? PbsNodeId { get; init; }
    public string TagNo { get; init; } = string.Empty;
    public string? LoopNo { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? TypeCode { get; init; }
    public string Discipline { get; init; } = string.Empty;
    public string? Location { get; init; }
    public string? PipeEquipmentNo { get; init; }
    public int Quantity { get; init; } = 1;
    public string? Purpose { get; init; }

    public string? PidDrawingNo { get; init; }
    public string? SpecNo { get; init; }
    public string? SpecPageNo { get; init; }
    public string? LoopDrawingNo { get; init; }
    public string? InstallDrawingNo { get; init; }
    public string? ElectricalDrawingNo { get; init; }
    public string? HeatSupplyDrawingNo { get; init; }
    public string? AirSupplyDrawingNo { get; init; }
    public string? LayingDrawingNo { get; init; }
    public string? JunctionBoxDrawingNo { get; init; }

    public string? Keyword { get; init; }
    public string? IoSetting { get; init; }
    public string? IndexNo { get; init; }
    public int SortOrder { get; init; }
    public string? Description { get; init; }
    public string? ProcessCondition { get; init; }
    public string? Specification { get; init; }
}

public sealed record EquipmentDto : EquipmentUpsertRequest
{
    public Guid Id { get; init; }
    public Guid PlantId { get; init; }
    /// <summary>当前项目范围（登录时选定的项目）；设备本身归工厂所有。</summary>
    public Guid ProjectId { get; init; }
    public string? PbsNodeName { get; init; }
    public string? PbsPath { get; init; }
    public DateTime CreatedAt { get; init; }
}
