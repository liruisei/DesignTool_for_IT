namespace WinDesign.Contracts.Projects;

public sealed record ProjectDto
{
    public Guid Id { get; init; }
    public Guid PlantId { get; init; }
    public string? PlantName { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ClientName { get; init; }
    public string? Location { get; init; }
    public string? Phase { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? OwnerUserId { get; init; }
    public string? OwnerName { get; init; }

    /// <summary>项目所在的 PBS 根节点（工厂顶层节点）。</summary>
    public Guid? PbsRootNodeId { get; init; }
    public string? PbsRootNodeName { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record CreateProjectRequest
{
    /// <summary>管理模块（未选择工作区工厂）创建项目时必填；工作区模式下由令牌决定。</summary>
    public Guid? PlantId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ClientName { get; init; }
    public string? Location { get; init; }
    public string? Phase { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? Description { get; init; }

    /// <summary>项目所在的 PBS 根节点，必须是所属工厂的顶层节点。</summary>
    public Guid? PbsRootNodeId { get; init; }
}

public sealed record UpdateProjectRequest
{
    public string Name { get; init; } = string.Empty;
    public string? ClientName { get; init; }
    public string? Location { get; init; }
    public string? Phase { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? Status { get; init; }
    public string? Description { get; init; }

    /// <summary>项目所在的 PBS 根节点，必须是所属工厂的顶层节点。</summary>
    public Guid? PbsRootNodeId { get; init; }
}
