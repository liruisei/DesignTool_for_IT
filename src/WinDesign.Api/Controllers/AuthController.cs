using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WinDesign.Api.Common;
using WinDesign.Api.Services;
using WinDesign.Contracts.Auth;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Workspaces;
using WinDesign.Domain.Entities;
using WinDesign.Infrastructure;

namespace WinDesign.Api.Controllers;

[Route("api/auth")]
public sealed class AuthController : ApiControllerBase
{
    private readonly WinDesignDbContext _db;
    private readonly IJwtTokenService _jwt;

    public AuthController(WinDesignDbContext db, IJwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ApiError("请输入用户名和密码"));
        }

        var user = await LoadUserAsync(
            x => x.UserName == request.UserName,
            ct);

        if (user is null || !user.IsActive || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new ApiError("用户名或密码错误"));
        }

        var roles = SelectRoles(user);
        var permissions = await SelectPermissionsAsync(user, ct);

        user.LastLoginAt = DateTime.UtcNow;
        _db.AddAudit(user.Id, user.UserName, "Login", "User", user.Id.ToString());
        await _db.SaveChangesAsync(ct);

        // 登录令牌只承载身份与权限；工厂 / 项目在用户完成工作区选择后写入新令牌。
        var (token, expiresAt) = _jwt.Create(user, roles, permissions);
        return Ok(new LoginResponse(
            token,
            expiresAt,
            new CurrentUserDto(user.Id, user.UserName, user.DisplayName, roles, permissions)));
    }

    /// <summary>
    /// 当前用户可访问的工厂列表，每个工厂下只列出用户参与的项目。
    /// </summary>
    [HttpGet("workspaces")]
    public async Task<ActionResult<IReadOnlyList<WorkspacePlantDto>>> GetWorkspaces(CancellationToken ct)
    {
        var userId = CurrentUserId;
        var isAdmin = User.IsInRole("Admin");

        var plants = await _db.Plants
            .Include(x => x.Projects)
                .ThenInclude(x => x.Members)
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .ToListAsync(ct);

        var result = plants
            .Select(plant => new WorkspacePlantDto(
                plant.Id,
                plant.Code,
                plant.Name,
                plant.ClientName,
                plant.Location,
                plant.Projects
                    .Where(project => isAdmin || project.Members.Any(m => m.UserId == userId))
                    .OrderBy(project => project.Code)
                    .Select(project => new WorkspaceProjectDto(
                        project.Id,
                        project.Code,
                        project.Name,
                        project.Phase,
                        project.Status.ToString()))
                    .ToList()))
            .Where(plant => isAdmin || plant.Projects.Count > 0)
            .ToList();

        return Ok(result);
    }

    /// <summary>
    /// 选择「工厂 → 项目」工作区，签发带 plantId / projectId 声明的新令牌。
    /// </summary>
    [HttpPost("workspace")]
    public async Task<ActionResult<WorkspaceResponse>> SelectWorkspace(
        [FromBody] SelectWorkspaceRequest request,
        CancellationToken ct)
    {
        var user = await LoadUserAsync(x => x.Id == CurrentUserId, ct);
        if (user is null || !user.IsActive)
        {
            return Unauthorized(new ApiError("用户不存在或已停用"));
        }

        var plant = await _db.Plants.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.PlantId, ct);
        if (plant is null)
        {
            return NotFound(new ApiError("工厂不存在"));
        }

        var project = await _db.Projects.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.ProjectId && x.PlantId == request.PlantId, ct);
        if (project is null)
        {
            return NotFound(new ApiError("项目不存在或不属于所选工厂"));
        }

        var isAdmin = user.IsSystemAdmin || user.UserRoles.Any(x =>
            string.Equals(x.Role.Code, "Admin", StringComparison.OrdinalIgnoreCase));
        if (!isAdmin)
        {
            var isMember = await _db.ProjectMembers
                .AnyAsync(x => x.ProjectId == project.Id && x.UserId == user.Id, ct);
            if (!isMember)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new ApiError("没有访问该项目的权限"));
            }
        }

        var roles = SelectRoles(user);
        var permissions = await SelectPermissionsAsync(user, ct);
        var (token, expiresAt) = _jwt.Create(user, roles, permissions, plant.Id, project.Id);

        var projectDto = new WorkspaceProjectDto(
            project.Id,
            project.Code,
            project.Name,
            project.Phase,
            project.Status.ToString());
        var plantDto = new WorkspacePlantDto(
            plant.Id,
            plant.Code,
            plant.Name,
            plant.ClientName,
            plant.Location,
            new[] { projectDto });

        _db.AddAudit(user.Id, user.UserName, "SelectWorkspace", "Workspace",
            project.Id.ToString(), $"{plant.Code} / {project.Code}");
        await _db.SaveChangesAsync(ct);

        return Ok(new WorkspaceResponse(
            token,
            expiresAt,
            new CurrentUserDto(user.Id, user.UserName, user.DisplayName, roles, permissions),
            plantDto,
            projectDto));
    }

    /// <summary>
    /// 管理员进入管理模块：不绑定工厂/项目，仅保留身份与权限，用于系统配置。
    /// </summary>
    [HttpPost("management")]
    public async Task<ActionResult<ManagementSessionResponse>> EnterManagement(CancellationToken ct)
    {
        if (!User.IsInRole("Admin"))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("只有管理员可以进入管理模块"));
        }

        var user = await LoadUserAsync(x => x.Id == CurrentUserId, ct);
        if (user is null || !user.IsActive)
        {
            return Unauthorized(new ApiError("用户不存在或已停用"));
        }

        var roles = SelectRoles(user);
        var permissions = await SelectPermissionsAsync(user, ct);
        var (token, expiresAt) = _jwt.Create(user, roles, permissions);

        _db.AddAudit(user.Id, user.UserName, "EnterManagement", "Workspace", user.Id.ToString());
        await _db.SaveChangesAsync(ct);

        return Ok(new ManagementSessionResponse(
            token,
            expiresAt,
            new CurrentUserDto(user.Id, user.UserName, user.DisplayName, roles, permissions)));
    }

    [HttpGet("me")]
    public ActionResult<CurrentUserDto> Me()
    {
        var roles = User.FindAll(ClaimTypes.Role)
            .Select(x => x.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var displayName = User.FindFirstValue("displayName") ?? CurrentUserName;
        return Ok(new CurrentUserDto(CurrentUserId, CurrentUserName, displayName, roles, CurrentPermissions));
    }

    private Task<User?> LoadUserAsync(
        System.Linq.Expressions.Expression<Func<User, bool>> predicate,
        CancellationToken ct) =>
        _db.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                    .ThenInclude(x => x.RolePermissions)
                        .ThenInclude(x => x.Permission)
            .SingleOrDefaultAsync(predicate, ct);

    private static List<string> SelectRoles(User user) =>
        user.UserRoles
            .Select(x => x.Role.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private async Task<List<string>> SelectPermissionsAsync(User user, CancellationToken ct) =>
        user.IsSystemAdmin
            ? await _db.Permissions.Select(x => x.Code).ToListAsync(ct)
            : user.UserRoles
                .SelectMany(x => x.Role.RolePermissions)
                .Select(x => x.Permission.Code)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
}
