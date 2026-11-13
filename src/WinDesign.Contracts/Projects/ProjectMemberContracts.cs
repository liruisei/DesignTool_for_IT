namespace WinDesign.Contracts.Projects;

/// <summary>
/// 项目成员。成员身份决定该用户在设计客户端能看到哪个项目及其 PBS 范围。
/// 项目负责人（<see cref="ProjectDto.OwnerUserId"/>）始终是成员，且不能移除或降级。
/// </summary>
public sealed record ProjectMemberDto(
    Guid UserId,
    string UserName,
    string DisplayName,
    string RoleInProject,
    bool IsActive);

/// <summary>添加项目成员或修改成员角色。可分配角色为 Manager / Designer / Viewer。</summary>
public sealed record UpsertProjectMemberRequest(Guid UserId, string RoleInProject);

/// <summary>批量授权：给一组用户授予同一个项目角色（管理/设计/只读）。</summary>
public sealed record BulkGrantProjectMembersRequest(
    IReadOnlyList<Guid> UserIds,
    string RoleInProject);
