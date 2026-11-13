using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WinDesign.Api.Common;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Permissions;
using WinDesign.Contracts.Projects;
using WinDesign.Contracts.Users;
using WinDesign.Domain.Entities;
using WinDesign.Infrastructure;

namespace WinDesign.Api.Controllers;

[Route("api/users")]
[Authorize(Roles = "Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminClient)]
public sealed class UsersController : ApiControllerBase
{
    private readonly WinDesignDbContext _db;

    public UsersController(WinDesignDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RequirePermission("users.read")]
    public async Task<ActionResult<PagedResult<UserDto>>> Get(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x => x.UserName.Contains(keyword) || x.DisplayName.Contains(keyword));
        }

        var total = await query.CountAsync(ct);
        var users = await query
            .OrderBy(x => x.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new PagedResult<UserDto>(users.Select(ToDto).ToList(), total, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("users.read")]
    public async Task<ActionResult<UserDto>> GetById(Guid id, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct);

        return user is null ? NotFound(new ApiError("用户不存在")) : Ok(ToDto(user));
    }

    [HttpPost]
    [RequirePermission("users.create")]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var userName = request.UserName.Trim();
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest(new ApiError("用户名和显示名称不能为空"));
        }
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return BadRequest(new ApiError("密码至少 6 位"));
        }
        if (await _db.Users.AnyAsync(x => x.UserName == userName, ct))
        {
            return Conflict(new ApiError("用户名已存在"));
        }

        var wanted = NormalizeRoles(request.Roles);
        var roles = await _db.Roles.Where(x => wanted.Contains(x.Code)).ToListAsync(ct);
        if (roles.Count != wanted.Count)
        {
            return BadRequest(new ApiError("包含不存在的角色"));
        }

        var user = new User
        {
            UserName = userName,
            DisplayName = request.DisplayName.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            PasswordHash = PasswordHasher.Hash(request.Password)
        };
        _db.Users.Add(user);
        foreach (var role in roles)
        {
            _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        }
        _db.AddAudit(CurrentUserId, CurrentUserName, "Create", "User", user.Id.ToString(), userName);
        await _db.SaveChangesAsync(ct);

        user.UserRoles = roles.Select(r => new UserRole { UserId = user.Id, RoleId = r.Id, Role = r }).ToList();
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, ToDto(user));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("users.update")]
    public async Task<ActionResult<UserDto>> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var user = await _db.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null)
        {
            return NotFound(new ApiError("用户不存在"));
        }
        if (id == CurrentUserId && !request.IsActive)
        {
            return BadRequest(new ApiError("不能停用当前登录用户"));
        }

        var wanted = NormalizeRoles(request.Roles);
        var roles = await _db.Roles.Where(x => wanted.Contains(x.Code)).ToListAsync(ct);
        if (roles.Count != wanted.Count)
        {
            return BadRequest(new ApiError("包含不存在的角色"));
        }

        var removesAdmin = user.IsSystemAdmin &&
                           (!request.IsActive || !roles.Any(x => x.Code == "Admin"));
        if (removesAdmin && !await HasAnotherActiveAdminAsync(id, ct))
        {
            return BadRequest(new ApiError("系统至少保留一个启用的管理员"));
        }

        user.DisplayName = request.DisplayName.Trim();
        user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        _db.UserRoles.RemoveRange(user.UserRoles);
        foreach (var role in roles)
        {
            _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        }

        _db.AddAudit(CurrentUserId, CurrentUserName, "Update", "User", user.Id.ToString(), user.UserName);
        await _db.SaveChangesAsync(ct);

        user.UserRoles = roles.Select(r => new UserRole { UserId = user.Id, RoleId = r.Id, Role = r }).ToList();
        return Ok(ToDto(user));
    }

    /// <summary>
    /// 删除用户。当前用户和最后一个启用的管理员不能被删除。
    /// 该用户名下的项目负责人自动转给当前操作的管理员，并补授该管理员「管理」项目角色。
    /// </summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission("users.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (id == CurrentUserId)
        {
            return BadRequest(new ApiError("不能删除当前登录用户"));
        }

        var user = await _db.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null)
        {
            return NotFound(new ApiError("用户不存在"));
        }

        var isActiveAdmin = user.IsActive &&
                            user.UserRoles.Any(x => string.Equals(x.Role.Code, "Admin", StringComparison.OrdinalIgnoreCase));
        if (isActiveAdmin && !await HasAnotherActiveAdminAsync(id, ct))
        {
            return BadRequest(new ApiError("系统至少保留一个启用的管理员"));
        }

        // 项目负责人转给当前操作的管理员；同时保证其成为该项目的「管理」成员。
        var ownedProjects = await _db.Projects
            .Where(x => x.OwnerUserId == id)
            .ToListAsync(ct);
        foreach (var project in ownedProjects)
        {
            project.OwnerUserId = CurrentUserId;
            project.UpdatedAt = DateTime.UtcNow;

            var newOwner = await _db.ProjectMembers
                .SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.UserId == CurrentUserId, ct);
            if (newOwner is null)
            {
                _db.ProjectMembers.Add(new ProjectMember
                {
                    ProjectId = project.Id,
                    UserId = CurrentUserId,
                    RoleInProject = ProjectRoleCatalog.Manager
                });
            }
            else
            {
                newOwner.RoleInProject = ProjectRoleCatalog.Manager;
            }
        }

        var memberships = await _db.ProjectMembers
            .Where(x => x.UserId == id)
            .ToListAsync(ct);
        _db.ProjectMembers.RemoveRange(memberships);
        _db.UserRoles.RemoveRange(user.UserRoles);
        _db.Users.Remove(user);

        _db.AddAudit(CurrentUserId, CurrentUserName, "Delete", "User", id.ToString(), user.UserName);
        if (ownedProjects.Count > 0)
        {
            _db.AddAudit(CurrentUserId, CurrentUserName, "TransferProjects", "User", id.ToString(),
                user.UserName + " → " + CurrentUserName + "：" + string.Join("、", ownedProjects.Select(x => x.Code)));
        }
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reset-password")]
    [RequirePermission("users.update")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            return BadRequest(new ApiError("密码至少 6 位"));
        }

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null)
        {
            return NotFound(new ApiError("用户不存在"));
        }

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        _db.AddAudit(CurrentUserId, CurrentUserName, "ResetPassword", "User", user.Id.ToString(), user.UserName);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/toggle-active")]
    [RequirePermission("users.update")]
    public async Task<ActionResult<UserDto>> ToggleActive(Guid id, CancellationToken ct)
    {
        if (id == CurrentUserId)
        {
            return BadRequest(new ApiError("不能停用当前登录用户"));
        }

        var user = await _db.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (user is null)
        {
            return NotFound(new ApiError("用户不存在"));
        }
        if (user.IsActive && user.IsSystemAdmin && !await HasAnotherActiveAdminAsync(id, ct))
        {
            return BadRequest(new ApiError("系统至少保留一个启用的管理员"));
        }

        user.IsActive = !user.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        _db.AddAudit(CurrentUserId, CurrentUserName, user.IsActive ? "Enable" : "Disable", "User", user.Id.ToString(), user.UserName);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(user));
    }

    private async Task<bool> HasAnotherActiveAdminAsync(Guid excludeUserId, CancellationToken ct)
    {
        return await _db.UserRoles
            .Where(x => x.Role.Code == "Admin" && x.UserId != excludeUserId && x.User.IsActive)
            .AnyAsync(ct);
    }

    private static List<string> NormalizeRoles(IReadOnlyList<string>? roles)
    {
        var list = roles is null
            ? new List<string>()
            : roles.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return list.Count == 0 ? new List<string> { "Viewer" } : list;
    }

    private static UserDto ToDto(User user) => new(
        user.Id,
        user.UserName,
        user.DisplayName,
        user.Email,
        user.Phone,
        user.IsActive,
        user.IsSystemAdmin,
        user.UserRoles.Select(x => x.Role.Code).OrderBy(x => x).ToList(),
        user.CreatedAt,
        user.LastLoginAt);
}
