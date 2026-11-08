using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WinDesign.Contracts.Common;

namespace WinDesign.Api.Common;

[ApiController]
[Authorize]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    protected string CurrentUserName => User.Identity?.Name ?? "unknown";

    protected IReadOnlyList<string> CurrentPermissions =>
        User.FindAll("permission").Select(x => x.Value).ToList();

    /// <summary>登录时选择的工作区工厂（PBS 根节点）。未选择工作区时为 null。</summary>
    protected Guid? CurrentPlantId =>
        Guid.TryParse(User.FindFirstValue("plantId"), out var id) ? id : null;

    /// <summary>登录时选择的工作区项目。</summary>
    protected Guid? CurrentProjectId =>
        Guid.TryParse(User.FindFirstValue("projectId"), out var id) ? id : null;

    protected bool IsSystemAdmin => User.IsInRole("Admin");

    /// <summary>
    /// 校验请求中的项目是否就是当前工作区项目；不匹配时返回 403。
    /// </summary>
    protected ActionResult? EnsureWorkspaceProject(Guid projectId)
    {
        if (CurrentProjectId is null)
        {
            return BadRequest(new ApiError("请先选择工作区（工厂与项目）"));
        }

        if (CurrentProjectId != projectId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("只能访问登录工作区中选定的项目"));
        }

        return null;
    }

    /// <summary>
    /// 校验请求中的工厂是否就是当前工作区工厂；不匹配时返回 403。
    /// </summary>
    protected ActionResult? EnsureWorkspacePlant(Guid plantId)
    {
        if (CurrentPlantId is null)
        {
            return BadRequest(new ApiError("请先选择工作区（工厂与项目）"));
        }

        if (CurrentPlantId != plantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("只能访问登录工作区中选定的工厂"));
        }

        return null;
    }
}
