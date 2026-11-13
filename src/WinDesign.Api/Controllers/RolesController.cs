using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WinDesign.Api.Common;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Permissions;
using WinDesign.Contracts.Users;
using WinDesign.Domain.Entities;
using WinDesign.Infrastructure;

namespace WinDesign.Api.Controllers;

/// <summary>
/// 用户类（角色）及其全局 CRUD 权限。用户类固定为 Admin / Designer / Viewer，不允许增删。
/// </summary>
[Route("api/roles")]
[Authorize(Policy = AuthorizationPolicies.AdminClient)]
[Authorize(Roles = "Admin")]
public sealed class RolesController : ApiControllerBase
{
    private readonly WinDesignDbContext _db;

    public RolesController(WinDesignDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RequirePermission("users.read")]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> Get(CancellationToken ct)
    {
        var roles = await _db.Roles
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new RoleDto(x.Id, x.Code, x.Name, x.Description))
            .ToListAsync(ct);
        return Ok(roles);
    }

    [HttpGet("{id:guid}/permissions")]
    [RequirePermission("users.read")]
    public async Task<ActionResult<RolePermissionDto>> GetPermissions(Guid id, CancellationToken ct)
    {
        var role = await _db.Roles
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (role is null)
        {
            return NotFound(new ApiError("用户类不存在"));
        }

        var codes = await _db.RolePermissions
            .Where(x => x.RoleId == id)
            .Select(x => x.Permission.Code)
            .ToListAsync(ct);

        return Ok(new RolePermissionDto(role.Id, role.Code, codes));
    }

    /// <summary>保存某个用户类的全局增删改查权限。保存后该类的用户需重新登录才会获得新令牌。</summary>
    [HttpPut("{id:guid}/permissions")]
    [RequirePermission("users.update")]
    public async Task<ActionResult<RolePermissionDto>> UpdatePermissions(
        Guid id,
        [FromBody] UpdateRolePermissionsRequest request,
        CancellationToken ct)
    {
        var role = await _db.Roles
            .Include(x => x.RolePermissions)
                .ThenInclude(x => x.Permission)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (role is null)
        {
            return NotFound(new ApiError("用户类不存在"));
        }

        var allPermissions = await _db.Permissions.ToListAsync(ct);
        var byCode = allPermissions.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

        var requested = (request.PermissionCodes ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var unknown = requested.Where(x => !byCode.ContainsKey(x)).ToList();
        if (unknown.Count > 0)
        {
            return BadRequest(new ApiError("包含不存在的权限：" + string.Join("、", unknown)));
        }

        var normalized = requested.Select(x => byCode[x].Code).ToList();

        // 管理员类必须保留用户管理的最小权限，避免保存后无人能再进入权限管理。
        if (string.Equals(role.Code, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            var required = new[]
            {
                PermissionCatalog.Code("users", "read"),
                PermissionCatalog.Code("users", "update")
            };
            if (required.Any(code => !normalized.Contains(code, StringComparer.OrdinalIgnoreCase)))
            {
                return BadRequest(new ApiError("系统管理员类必须保留「用户管理 - 查看」和「用户管理 - 修改」权限"));
            }
        }

        var keep = normalized.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentCodes = role.RolePermissions
            .Select(x => x.Permission.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var link in role.RolePermissions.Where(x => !keep.Contains(x.Permission.Code)).ToList())
        {
            _db.RolePermissions.Remove(link);
        }
        foreach (var code in normalized.Where(x => !currentCodes.Contains(x)))
        {
            _db.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = byCode[code].Id
            });
        }

        _db.AddAudit(CurrentUserId, CurrentUserName, "UpdatePermissions", "Role", role.Id.ToString(),
            role.Code + " / " + normalized.Count + " 项");
        await _db.SaveChangesAsync(ct);

        return Ok(new RolePermissionDto(role.Id, role.Code, normalized));
    }
}
