using WinDesign.Domain.Common;

namespace WinDesign.Domain.Entities;

/// <summary>
/// 工厂 / 企业。PBS 的根节点就是工厂，一个工厂可以承载多个项目（或项目群）。
/// </summary>
public class Plant : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<PbsNode> PbsNodes { get; set; } = new List<PbsNode>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<Equipment> Equipments { get; set; } = new List<Equipment>();
}

/// <summary>
/// 项目。设备与 PBS 归工厂所有，项目只是设计 / 报表范围。
/// </summary>
public class Project : EntityBase
{
    public Guid PlantId { get; set; }
    public Plant Plant { get; set; } = null!;

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public string? Location { get; set; }
    public string? Phase { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public string? Description { get; set; }
    public Guid? OwnerUserId { get; set; }
    public User? Owner { get; set; }

    /// <summary>
    /// 项目所在的 PBS 根节点。为空表示项目放在工厂根节点上（范围为全厂）；
    /// 非空时必须是本工厂下的顶层节点（ParentId 为空）。
    /// 一个根节点可被多个项目共用。
    /// </summary>
    public Guid? PbsRootNodeId { get; set; }
    public PbsNode? PbsRootNode { get; set; }

    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public ICollection<ProjectEquipment> ProjectEquipments { get; set; } = new List<ProjectEquipment>();
}

public class ProjectMember
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string RoleInProject { get; set; } = "Designer";
}

/// <summary>
/// 项目—设备范围。设备归工厂，同一设备可纳入多个项目共享。
/// </summary>
public class ProjectEquipment
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 工厂 PBS 节点。根节点由 <see cref="Plant"/> 承载，本表只存工厂下的层级节点。
/// </summary>
public class PbsNode : EntityBase
{
    public Guid PlantId { get; set; }
    public Plant Plant { get; set; } = null!;
    public Guid? ParentId { get; set; }
    public PbsNode? Parent { get; set; }
    public ICollection<PbsNode> Children { get; set; } = new List<PbsNode>();

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PbsNodeType NodeType { get; set; } = PbsNodeType.System;
    public int SortOrder { get; set; }
    public string? Description { get; set; }
}

public class Equipment : EntityBase
{
    public Guid PlantId { get; set; }
    public Plant Plant { get; set; } = null!;
    public Guid? PbsNodeId { get; set; }
    public PbsNode? PbsNode { get; set; }

    public string TagNo { get; set; } = string.Empty;
    public string? LoopNo { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TypeCode { get; set; }
    public Discipline Discipline { get; set; } = Discipline.Instrument;
    public string? Location { get; set; }
    public string? PipeEquipmentNo { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Purpose { get; set; }

    public string? PidDrawingNo { get; set; }
    public string? SpecNo { get; set; }
    public string? SpecPageNo { get; set; }
    public string? LoopDrawingNo { get; set; }
    public string? InstallDrawingNo { get; set; }
    public string? ElectricalDrawingNo { get; set; }
    public string? HeatSupplyDrawingNo { get; set; }
    public string? AirSupplyDrawingNo { get; set; }
    public string? LayingDrawingNo { get; set; }
    public string? JunctionBoxDrawingNo { get; set; }

    public string? Keyword { get; set; }
    public string? IoSetting { get; set; }
    public string? IndexNo { get; set; }
    public int SortOrder { get; set; }
    public string? Description { get; set; }
    public string? ProcessCondition { get; set; }
    public string? Specification { get; set; }

    public ICollection<EquipmentSpec> Specs { get; set; } = new List<EquipmentSpec>();
    public ICollection<MaterialItem> Materials { get; set; } = new List<MaterialItem>();
    public ICollection<ProjectEquipment> ProjectEquipments { get; set; } = new List<ProjectEquipment>();
}

public class EquipmentSpec : EntityBase
{
    public Guid EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;
    public string SpecCode { get; set; } = string.Empty;
    public string SpecName { get; set; } = string.Empty;
    public string ContentJson { get; set; } = "{}";
    public int Version { get; set; } = 1;
}

/// <summary>
/// 交换机样本库：设计端电信统计按这里的端口规格计算交换机台数。
/// Access 用于信息点接入，PoE 用于无线 AP 接入。
/// </summary>
public class SwitchModel : EntityBase
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SwitchType SwitchType { get; set; } = SwitchType.Access;
    public int ElectricalPorts { get; set; } = 24;
    public int? UplinkPorts { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 项目电信统计的房间记录：建筑物来自 PBS 建筑物节点，楼层/房间为设计数据。
/// 电话点不占用交换机电口，只参与统计。
/// </summary>
public class TelecomRoom : EntityBase
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>关联的 PBS 建筑物节点（NodeType = Building）。</summary>
    public Guid PbsNodeId { get; set; }
    public PbsNode PbsNode { get; set; } = null!;

    public string Floor { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DataPointCount { get; set; }
    public int PhonePointCount { get; set; }
    public int ApCount { get; set; }
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}

/// <summary>
/// 项目级设备位号编号规定：
/// 前缀字段 + 分隔符 + 设备类型字段 + 分隔符 + 4 位顺序号 + 可选后缀。
/// 这里只定义“使用哪个字段”，具体值在生成位号时按项目数据填充。
/// </summary>
public class NumberingRule : EntityBase
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    /// <summary>前缀字段：UnitCode（装置编号）或空（允许手动输入）。</summary>
    public string PrefixField { get; set; } = string.Empty;
    public string PrefixSeparator { get; set; } = string.Empty;
    /// <summary>前缀字段长度。装置编号字段由数据长度决定，不可编辑。</summary>
    public int PrefixLength { get; set; } = 4;
    /// <summary>设备类型字段：EquipmentTypeCode（设备类型编码）或空（允许手动输入）。</summary>
    public string EquipmentTypeField { get; set; } = string.Empty;
    public string TypeSeparator { get; set; } = string.Empty;
    public int TypeLength { get; set; } = 5;
    /// <summary>顺序号长度，默认 4 位。</summary>
    public int SequenceLength { get; set; } = 4;
    /// <summary>顺序号后的可选后缀，通常为 A/B/C/D 等。</summary>
    public string Suffix { get; set; } = string.Empty;
    /// <summary>后缀字段长度。</summary>
    public int SuffixLength { get; set; } = 1;
}

public class MaterialItem : EntityBase
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid? EquipmentId { get; set; }
    public Equipment? Equipment { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Spec { get; set; }
    public string? Material { get; set; }
    public string Unit { get; set; } = "个";
    public decimal Quantity { get; set; }
    public string? Remark { get; set; }
}
