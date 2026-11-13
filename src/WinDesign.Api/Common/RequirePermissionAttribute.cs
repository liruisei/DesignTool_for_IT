using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using WinDesign.Contracts.Common;

namespace WinDesign.Api.Common;

/// <summary>
/// 要求当前令牌包含指定的全局权限码（如 equipment.create）。
/// 权限来自登录时用户类（角色）配置，保存在 JWT 的 permission 声明中。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : TypeFilterAttribute
{
    public RequirePermissionAttribute(string permission)
        : base(typeof(PermissionAuthorizationFilter))
    {
        Arguments = new object[] { permission };
    }
}

public sealed class PermissionAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly string _permission;

    public PermissionAuthorizationFilter(string permission)
    {
        _permission = permission;
    }

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        var allowed = user.Identity?.IsAuthenticated == true &&
                      user.FindAll("permission")
                          .Any(x => string.Equals(x.Value, _permission, StringComparison.OrdinalIgnoreCase));

        if (!allowed)
        {
            context.Result = new ObjectResult(new ApiError("没有权限执行该操作"))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        return Task.CompletedTask;
    }
}
