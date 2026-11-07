using WinDesign.Domain.Common;

namespace WinDesign.Domain.Entities;

public class Project : EntityBase
{
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

    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public ICollection<PbsNode> PbsNodes { get; set; } = new List<PbsNode>();
    public ICollection<Equipment> Equipments { get; set; } = new List<Equipment>();
}

public class ProjectMember
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string RoleInProject { get; set; } = "Designer";
}

public class PbsNode : EntityBase
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
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
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid? PbsNodeId { get; set; }
    public PbsNode? PbsNode { get; set; }

    public string TagNo { get; set; } = string.Empty;
    public string? LoopNo { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TypeCode { get; set; }
    public Discipline Discipline { get; set; } = Discipline.Instrument;
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Planned;
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
