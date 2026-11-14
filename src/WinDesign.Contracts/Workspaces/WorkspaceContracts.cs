namespace WinDesign.Contracts.Workspaces;

/// <summary>
/// 登录后可选的工厂。工厂是 PBS 根节点，Projects 为该用户在当前工厂下可访问的项目。
/// </summary>
public sealed record WorkspacePlantDto(
    Guid Id,
    string Code,
    string Name,
    string? ClientName,
    string? Location,
    IReadOnlyList<WorkspaceProjectDto> Projects);

public sealed record WorkspaceProjectDto(
    Guid Id,
    string Code,
    string Name,
    string? Phase,
    string Status)
{
    /// <summary>项目所在的 PBS 根节点；为空表示工厂根节点（全厂），设计端 PBS 树据此显示。</summary>
    public Guid? PbsRootNodeId { get; init; }

    /// <summary>项目 PBS 范围内的装置（Unit）清单，供登录时选择默认装置。</summary>
    public IReadOnlyList<WorkspaceUnitDto> Units { get; init; } = Array.Empty<WorkspaceUnitDto>();
}

/// <summary>工作区选择用的装置节点。</summary>
public sealed record WorkspaceUnitDto(Guid Id, string Code, string Name);

public sealed record SelectWorkspaceRequest(Guid PlantId, Guid ProjectId);

/// <summary>
/// 工作区选择结果：返回带 plantId / projectId 声明的新令牌，后续所有数据请求按该范围授权。
/// </summary>
public sealed record WorkspaceResponse(
    string AccessToken,
    DateTime ExpiresAt,
    Auth.CurrentUserDto User,
    WorkspacePlantDto Plant,
    WorkspaceProjectDto Project);

/// <summary>
/// 管理模块会话：不绑定工厂与项目，仅管理员可用，只有系统配置类功能。
/// </summary>
public sealed record ManagementSessionResponse(
    string AccessToken,
    DateTime ExpiresAt,
    Auth.CurrentUserDto User);
