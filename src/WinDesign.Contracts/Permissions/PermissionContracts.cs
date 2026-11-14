namespace WinDesign.Contracts.Permissions;

/// <summary>权限模块（全局功能域）。</summary>
public sealed record PermissionModuleDto(string Code, string Name);

/// <summary>权限动作：增、删、改、查。</summary>
public sealed record PermissionActionDto(string Code, string Name);

/// <summary>
/// 全局权限目录：模块 × 动作。
/// 权限码格式为 "module.action"，例如 equipment.create。
/// 管理端「权限管理」按该目录渲染 CRUD 矩阵；服务端按同一目录生成权限数据。
/// </summary>
public static class PermissionCatalog
{
    public static IReadOnlyList<PermissionModuleDto> Modules { get; } = new[]
    {
        new PermissionModuleDto("users", "用户管理"),
        new PermissionModuleDto("projects", "项目管理"),
        new PermissionModuleDto("dictionaries", "编码定义"),
        new PermissionModuleDto("pbs", "PBS 管理"),
        new PermissionModuleDto("equipment", "设备索引"),
        new PermissionModuleDto("specs", "设备规格"),
        new PermissionModuleDto("materials", "材料附件"),
        new PermissionModuleDto("telecom", "电信统计"),
        new PermissionModuleDto("switches", "设备样本库"),
        new PermissionModuleDto("numbering", "编号规定"),
        new PermissionModuleDto("reports", "报表输出")
    };

    public static IReadOnlyList<PermissionActionDto> Actions { get; } = new[]
    {
        new PermissionActionDto("create", "新增"),
        new PermissionActionDto("read", "查看"),
        new PermissionActionDto("update", "修改"),
        new PermissionActionDto("delete", "删除")
    };

    public static string Code(string module, string action) => module + "." + action;

    public static string DisplayName(string module, string action)
    {
        var moduleName = Modules.FirstOrDefault(x => x.Code == module)?.Name ?? module;
        var actionName = Actions.FirstOrDefault(x => x.Code == action)?.Name ?? action;
        return moduleName + " - " + actionName;
    }

    /// <summary>全部权限码（用于给 Admin 类初始化全部权限）。</summary>
    public static IEnumerable<string> AllCodes() =>
        Modules.SelectMany(module => Actions.Select(action => Code(module.Code, action.Code)));
}

/// <summary>某个用户类（角色）当前拥有的全局权限码。</summary>
public sealed record RolePermissionDto(Guid RoleId, string RoleCode, IReadOnlyList<string> PermissionCodes);

/// <summary>保存某个用户类的全局权限。保存后已登录用户需重新登录才能获得新令牌。</summary>
public sealed record UpdateRolePermissionsRequest(IReadOnlyList<string> PermissionCodes);
