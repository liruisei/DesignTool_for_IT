namespace WinDesign.Api.Common;

/// <summary>
/// 客户端类型授权策略。
/// 令牌中的 client 声明由 /api/auth/management（admin）与 /api/auth/workspace（design）写入。
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>只允许管理客户端令牌访问：用户 / 角色 / 工厂 / 项目 / PBS 写入 / 编码写入。</summary>
    public const string AdminClient = "AdminClient";

    /// <summary>只允许设计客户端令牌访问：索引表 / 设备等设计数据。</summary>
    public const string DesignClient = "DesignClient";

    /// <summary>管理端与设计端都可访问：PBS / 编码读取、工作台统计。</summary>
    public const string ClientAccess = "ClientAccess";
}
