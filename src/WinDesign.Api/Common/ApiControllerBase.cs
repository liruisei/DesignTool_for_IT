using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
}
