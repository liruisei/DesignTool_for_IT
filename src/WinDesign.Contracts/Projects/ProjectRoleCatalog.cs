namespace WinDesign.Contracts.Projects;

/// <summary>项目级角色：只控制项目准入与成员维护，具体增删改查由用户的全局用户类决定。</summary>
public static class ProjectRoleCatalog
{
    public const string Manager = "Manager";
    public const string Designer = "Designer";
    public const string Viewer = "Viewer";

    public static IReadOnlyList<ProjectRoleOptionDto> Options { get; } = new[]
    {
        new ProjectRoleOptionDto(Manager, "管理", "可进入项目、维护项目成员；设计数据按全局用户类权限执行"),
        new ProjectRoleOptionDto(Designer, "设计", "可进入项目并按全局用户类权限维护设计数据"),
        new ProjectRoleOptionDto(Viewer, "只读", "可进入项目并按全局用户类权限查看数据")
    };

    /// <summary>兼容旧数据：历史项目成员使用 Owner，统一显示为 Manager。</summary>
    public static string Normalize(string? role)
    {
        if (string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase))
        {
            return Manager;
        }

        return Options.FirstOrDefault(x => string.Equals(x.Code, role, StringComparison.OrdinalIgnoreCase))?.Code
               ?? Viewer;
    }

    public static bool IsValid(string? role) =>
        Options.Any(x => string.Equals(x.Code, role, StringComparison.OrdinalIgnoreCase));

    public static string DisplayName(string? role) =>
        Options.FirstOrDefault(x => string.Equals(x.Code, Normalize(role), StringComparison.OrdinalIgnoreCase))?.Name
        ?? Normalize(role);
}

public sealed record ProjectRoleOptionDto(string Code, string Name, string Description);
