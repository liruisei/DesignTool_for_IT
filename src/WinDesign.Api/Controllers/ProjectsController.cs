using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WinDesign.Api.Common;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Permissions;
using WinDesign.Contracts.Projects;
using WinDesign.Domain;
using WinDesign.Domain.Entities;
using WinDesign.Infrastructure;

namespace WinDesign.Api.Controllers;

[Route("api/projects")]
[Authorize(Policy = AuthorizationPolicies.AdminClient)]
public sealed class ProjectsController : ApiControllerBase
{
    private readonly WinDesignDbContext _db;

    public ProjectsController(WinDesignDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RequirePermission("projects.read")]
    public async Task<ActionResult<PagedResult<ProjectDto>>> Get(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var userId = CurrentUserId;
        var isAdmin = User.IsInRole("Admin");
        if (CurrentPlantId is null && !isAdmin)
        {
            return BadRequest(new ApiError("请先选择工作区工厂"));
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Projects
            .Include(x => x.Owner)
            .Include(x => x.Plant)
            .Include(x => x.PbsRootNode)
            .AsNoTracking();

        if (CurrentPlantId is Guid plantId)
        {
            query = query.Where(x => x.PlantId == plantId);
        }

        if (!isAdmin)
        {
            query = query.Where(x => x.Members.Any(m => m.UserId == userId));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x => x.Code.Contains(keyword) || x.Name.Contains(keyword) ||
                                     (x.ClientName != null && x.ClientName.Contains(keyword)));
        }
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ProjectStatus>(status, true, out var parsed))
        {
            query = query.Where(x => x.Status == parsed);
        }

        var total = await query.CountAsync(ct);
        var projects = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new PagedResult<ProjectDto>(projects.Select(ToDto).ToList(), total, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("projects.read")]
    public async Task<ActionResult<ProjectDto>> GetById(Guid id, CancellationToken ct)
    {
        var project = await _db.Projects
            .Include(x => x.Owner)
            .Include(x => x.Plant)
            .Include(x => x.PbsRootNode)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct);

        if (project is null)
        {
            return NotFound(new ApiError("项目不存在"));
        }
        if (CurrentPlantId is Guid plantId && project.PlantId != plantId)
        {
            return NotFound(new ApiError("项目不存在"));
        }
        if (!await CanAccessAsync(project.Id, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError("没有访问该项目的权限"));
        }

        return Ok(ToDto(project));
    }

    // ---------- 项目成员 ----------

    /// <summary>项目成员列表。项目负责人排在最前，便于界面识别不可移除的成员。</summary>
    [HttpGet("{id:guid}/members")]
    [RequirePermission("projects.read")]
    public async Task<ActionResult<IReadOnlyList<ProjectMemberDto>>> GetMembers(Guid id, CancellationToken ct)
    {
        var project = await _db.Projects.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (project is null)
        {
            return NotFound(new ApiError("项目不存在"));
        }
        if (CurrentPlantId is Guid plantId && project.PlantId != plantId)
        {
            return NotFound(new ApiError("项目不存在"));
        }
        if (!await CanAccessAsync(project.Id, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError("没有访问该项目的权限"));
        }

        var members = await _db.ProjectMembers
            .Include(x => x.User)
            .AsNoTracking()
            .Where(x => x.ProjectId == id)
            .ToListAsync(ct);

        var result = members
            .OrderBy(x => x.UserId == project.OwnerUserId ? 0 : 1)
            .ThenBy(x => x.User.UserName, StringComparer.OrdinalIgnoreCase)
            .Select(ToMemberDto)
            .ToList();

        return Ok(result);
    }

    /// <summary>新增项目成员。只有管理员可以分配成员。</summary>
    [HttpPost("{id:guid}/members")]
    [Authorize(Roles = "Admin")]
    [RequirePermission("projects.update")]
    public async Task<ActionResult<ProjectMemberDto>> AddMember(
        Guid id,
        [FromBody] UpsertProjectMemberRequest request,
        CancellationToken ct)
    {
        var project = await _db.Projects.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (project is null)
        {
            return NotFound(new ApiError("项目不存在"));
        }
        if (CurrentPlantId is Guid plantId && project.PlantId != plantId)
        {
            return NotFound(new ApiError("项目不存在"));
        }
        if (!await CanAccessAsync(project.Id, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError("没有访问该项目的权限"));
        }

        var roleError = ValidateAssignableRole(request.RoleInProject, out var role);
        if (roleError is not null)
        {
            return BadRequest(new ApiError(roleError));
        }

        var user = await _db.Users.SingleOrDefaultAsync(x => x.Id == request.UserId, ct);
        if (user is null)
        {
            return NotFound(new ApiError("用户不存在"));
        }
        if (!user.IsActive)
        {
            return BadRequest(new ApiError("用户已停用，不能加入项目"));
        }
        if (user.Id == project.OwnerUserId ||
            await _db.ProjectMembers.AnyAsync(x => x.ProjectId == id && x.UserId == user.Id, ct))
        {
            return Conflict(new ApiError("该用户已是项目成员"));
        }

        var member = new ProjectMember
        {
            ProjectId = id,
            UserId = user.Id,
            RoleInProject = role
        };
        _db.ProjectMembers.Add(member);
        _db.AddAudit(CurrentUserId, CurrentUserName, "AddMember", "Project", id.ToString(),
            $"{user.UserName} / {role}");
        await _db.SaveChangesAsync(ct);

        return Ok(new ProjectMemberDto(user.Id, user.UserName, user.DisplayName, role, user.IsActive));
    }

    /// <summary>修改非负责人成员的项目角色。</summary>
    [HttpPut("{id:guid}/members/{userId:guid}")]
    [Authorize(Roles = "Admin")]
    [RequirePermission("projects.update")]
    public async Task<ActionResult<ProjectMemberDto>> UpdateMemberRole(
        Guid id,
        Guid userId,
        [FromBody] UpsertProjectMemberRequest request,
        CancellationToken ct)
    {
        var project = await _db.Projects.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (project is null)
        {
            return NotFound(new ApiError("项目不存在"));
        }
        if (CurrentPlantId is Guid plantId && project.PlantId != plantId)
        {
            return NotFound(new ApiError("项目不存在"));
        }
        if (!await CanAccessAsync(project.Id, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError("没有访问该项目的权限"));
        }
        if (userId == project.OwnerUserId)
        {
            return BadRequest(new ApiError("项目负责人的角色不能修改"));
        }

        var member = await _db.ProjectMembers
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.ProjectId == id && x.UserId == userId, ct);
        if (member is null)
        {
            return NotFound(new ApiError("该用户不是项目成员"));
        }

        var roleError = ValidateAssignableRole(request.RoleInProject, out var role);
        if (roleError is not null)
        {
            return BadRequest(new ApiError(roleError));
        }

        member.RoleInProject = role;
        _db.AddAudit(CurrentUserId, CurrentUserName, "UpdateMember", "Project", id.ToString(),
            $"{member.User.UserName} / {role}");
        await _db.SaveChangesAsync(ct);

        return Ok(new ProjectMemberDto(
            member.UserId,
            member.User.UserName,
            member.User.DisplayName,
            member.RoleInProject,
            member.User.IsActive));
    }

    /// <summary>移除项目成员。项目负责人不能被移除。</summary>
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [Authorize(Roles = "Admin")]
    [RequirePermission("projects.update")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        var project = await _db.Projects.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (project is null)
        {
            return NotFound(new ApiError("项目不存在"));
        }
        if (CurrentPlantId is Guid plantId && project.PlantId != plantId)
        {
            return NotFound(new ApiError("项目不存在"));
        }
        if (!await CanAccessAsync(project.Id, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError("没有访问该项目的权限"));
        }
        if (userId == project.OwnerUserId)
        {
            return BadRequest(new ApiError("项目负责人不能被移出项目"));
        }

        var member = await _db.ProjectMembers
            .SingleOrDefaultAsync(x => x.ProjectId == id && x.UserId == userId, ct);
        if (member is null)
        {
            return NotFound(new ApiError("该用户不是项目成员"));
        }

        _db.ProjectMembers.Remove(member);
        _db.AddAudit(CurrentUserId, CurrentUserName, "RemoveMember", "Project", id.ToString(),
            member.UserId.ToString());
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>
    /// 批量授权：勾选一组用户，统一授予管理 / 设计 / 只读项目角色。
    /// 已是成员的用户会被更新为所选角色；项目负责人必须保持“管理”。
    /// </summary>
    [HttpPost("{id:guid}/members/bulk")]
    [Authorize(Roles = "Admin")]
    [RequirePermission("projects.update")]
    public async Task<ActionResult<IReadOnlyList<ProjectMemberDto>>> BulkGrantMembers(
        Guid id,
        [FromBody] BulkGrantProjectMembersRequest request,
        CancellationToken ct)
    {
        var project = await _db.Projects.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (project is null)
        {
            return NotFound(new ApiError("项目不存在"));
        }
        if (CurrentPlantId is Guid plantId && project.PlantId != plantId)
        {
            return NotFound(new ApiError("项目不存在"));
        }
        if (!await CanAccessAsync(project.Id, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError("没有访问该项目的权限"));
        }

        var roleError = ValidateAssignableRole(request.RoleInProject, out var role);
        if (roleError is not null)
        {
            return BadRequest(new ApiError(roleError));
        }

        var userIds = (request.UserIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (userIds.Count == 0)
        {
            return BadRequest(new ApiError("请至少选择一个用户"));
        }

        var users = await _db.Users.Where(x => userIds.Contains(x.Id)).ToListAsync(ct);
        if (users.Count != userIds.Count)
        {
            return BadRequest(new ApiError("包含不存在的用户"));
        }
        var inactive = users.Where(x => !x.IsActive).Select(x => x.UserName).ToList();
        if (inactive.Count > 0)
        {
            return BadRequest(new ApiError("以下用户已停用，不能授权：" + string.Join("、", inactive)));
        }

        if (project.OwnerUserId is Guid ownerId &&
            userIds.Contains(ownerId) &&
            !string.Equals(role, ProjectRoleCatalog.Manager, StringComparison.Ordinal))
        {
            return BadRequest(new ApiError("项目负责人必须保持“管理”角色"));
        }

        var existing = await _db.ProjectMembers
            .Where(x => x.ProjectId == id && userIds.Contains(x.UserId))
            .ToListAsync(ct);

        foreach (var user in users)
        {
            var member = existing.FirstOrDefault(x => x.UserId == user.Id);
            if (member is null)
            {
                _db.ProjectMembers.Add(new ProjectMember
                {
                    ProjectId = id,
                    UserId = user.Id,
                    RoleInProject = role
                });
            }
            else
            {
                member.RoleInProject = role;
            }
        }

        _db.AddAudit(CurrentUserId, CurrentUserName, "BulkGrantMembers", "Project", id.ToString(),
            $"{users.Count} 人 / {role}");
        await _db.SaveChangesAsync(ct);

        var members = await _db.ProjectMembers
            .Include(x => x.User)
            .AsNoTracking()
            .Where(x => x.ProjectId == id)
            .ToListAsync(ct);

        var result = members
            .OrderBy(x => x.UserId == project.OwnerUserId ? 0 : 1)
            .ThenBy(x => x.User.UserName, StringComparer.OrdinalIgnoreCase)
            .Select(ToMemberDto)
            .ToList();
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("projects.create")]
    public async Task<ActionResult<ProjectDto>> Create([FromBody] CreateProjectRequest request, CancellationToken ct)
    {
        var isAdmin = User.IsInRole("Admin");
        var plantId = CurrentPlantId ?? request.PlantId;
        if (plantId is null)
        {
            return BadRequest(new ApiError("请选择项目所属工厂"));
        }
        if (CurrentPlantId is null && !isAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("只有管理员可以在管理模块下创建项目"));
        }
        if (!await _db.Plants.AnyAsync(x => x.Id == plantId.Value, ct))
        {
            return NotFound(new ApiError("工厂不存在"));
        }

        var code = request.Code.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiError("项目代号和项目名称不能为空"));
        }
        if (await _db.Projects.AnyAsync(x => x.PlantId == plantId.Value && x.Code == code, ct))
        {
            return Conflict(new ApiError("该工厂下项目代号已存在"));
        }
        var rootError = await ValidatePbsRootAsync(request.PbsRootNodeId, plantId.Value, ct);
        if (rootError is not null)
        {
            return BadRequest(new ApiError(rootError));
        }

        var project = new Project
        {
            PbsRootNodeId = request.PbsRootNodeId,
            PlantId = plantId.Value,
            Code = code,
            Name = request.Name.Trim(),
            ClientName = string.IsNullOrWhiteSpace(request.ClientName) ? null : request.ClientName.Trim(),
            Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            Phase = string.IsNullOrWhiteSpace(request.Phase) ? null : request.Phase.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            OwnerUserId = CurrentUserId,
            Status = ProjectStatus.Active
        };
        _db.Projects.Add(project);
        _db.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = CurrentUserId,
            RoleInProject = ProjectRoleCatalog.Manager
        });
        _db.AddAudit(CurrentUserId, CurrentUserName, "Create", "Project", project.Id.ToString(), code);
        await _db.SaveChangesAsync(ct);

        var created = await _db.Projects
            .Include(x => x.Owner)
            .Include(x => x.Plant)
            .Include(x => x.PbsRootNode)
            .AsNoTracking()
            .SingleAsync(x => x.Id == project.Id, ct);
        return CreatedAtAction(nameof(GetById), new { id = project.Id }, ToDto(created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("projects.update")]
    public async Task<ActionResult<ProjectDto>> Update(Guid id, [FromBody] UpdateProjectRequest request, CancellationToken ct)
    {
        var project = await _db.Projects
            .Include(x => x.Owner)
            .Include(x => x.Plant)
            .Include(x => x.PbsRootNode)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (project is null)
        {
            return NotFound(new ApiError("项目不存在"));
        }
        if (CurrentPlantId is Guid plantId && project.PlantId != plantId)
        {
            return NotFound(new ApiError("项目不存在"));
        }
        if (CurrentPlantId is null && !User.IsInRole("Admin"))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("只有管理员可以在管理模块下编辑项目"));
        }
        if (!await CanAccessAsync(project.Id, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError("没有访问该项目的权限"));
        }

        var status = project.Status;
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            !Enum.TryParse<ProjectStatus>(request.Status, true, out status))
        {
            return BadRequest(new ApiError("项目状态无效"));
        }
        var rootError = await ValidatePbsRootAsync(request.PbsRootNodeId, project.PlantId, ct);
        if (rootError is not null)
        {
            return BadRequest(new ApiError(rootError));
        }

        project.PbsRootNodeId = request.PbsRootNodeId;
        project.Name = request.Name.Trim();
        project.ClientName = string.IsNullOrWhiteSpace(request.ClientName) ? null : request.ClientName.Trim();
        project.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        project.Phase = string.IsNullOrWhiteSpace(request.Phase) ? null : request.Phase.Trim();
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;
        project.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        project.Status = status;
        project.UpdatedAt = DateTime.UtcNow;

        _db.AddAudit(CurrentUserId, CurrentUserName, "Update", "Project", project.Id.ToString(), project.Code);
        await _db.SaveChangesAsync(ct);

        // 重新加载根节点，保证返回的 DTO 带有根节点名称。
        await _db.Entry(project).Reference(x => x.PbsRootNode).LoadAsync(ct);
        return Ok(ToDto(project));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [RequirePermission("projects.delete")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var project = await _db.Projects.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (project is null)
        {
            return NotFound(new ApiError("项目不存在"));
        }
        if (CurrentPlantId is Guid plantId && project.PlantId != plantId)
        {
            return NotFound(new ApiError("项目不存在"));
        }
        if (CurrentPlantId is null && !User.IsInRole("Admin"))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("只有管理员可以在管理模块下归档项目"));
        }

        project.Status = ProjectStatus.Archived;
        project.UpdatedAt = DateTime.UtcNow;
        _db.AddAudit(CurrentUserId, CurrentUserName, "Archive", "Project", project.Id.ToString(), project.Code);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<bool> CanAccessAsync(Guid projectId, CancellationToken ct) =>
        User.IsInRole("Admin") ||
        await _db.ProjectMembers.AnyAsync(x => x.ProjectId == projectId && x.UserId == CurrentUserId, ct);

    /// <summary>
    /// 校验项目 PBS 根节点：为空表示项目范围为整个工厂（工厂即根节点），允许；
    /// 非空时必须属于该工厂，且必须是顶层节点（ParentId 为空）。
    /// 返回错误信息；null 表示通过。
    /// </summary>
    private async Task<string?> ValidatePbsRootAsync(Guid? rootNodeId, Guid plantId, CancellationToken ct)
    {
        if (rootNodeId is null)
        {
            return null;
        }

        var root = await _db.PbsNodes
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == rootNodeId.Value && x.PlantId == plantId, ct);
        if (root is null)
        {
            return "所选的 PBS 根节点不存在或不属于该项目所属工厂";
        }
        if (root.ParentId is not null)
        {
            return "项目 PBS 根节点必须是工厂下的顶层节点";
        }

        return null;
    }

    /// <summary>校验可分配的项目角色：管理 / 设计 / 只读；历史 Owner 归一为管理。</summary>
    private static string? ValidateAssignableRole(string? role, out string normalized)
    {
        if (!ProjectRoleCatalog.IsValid(role) &&
            !string.Equals(role?.Trim(), "Owner", StringComparison.OrdinalIgnoreCase))
        {
            normalized = string.Empty;
            return "项目角色只能是 Manager（管理）、Designer（设计）或 Viewer（只读）";
        }

        normalized = ProjectRoleCatalog.Normalize(role);
        return null;
    }

    private static ProjectMemberDto ToMemberDto(ProjectMember member) => new(
        member.UserId,
        member.User.UserName,
        member.User.DisplayName,
        ProjectRoleCatalog.Normalize(member.RoleInProject),
        member.User.IsActive);

    private static ProjectDto ToDto(Project project) => new()
    {
        Id = project.Id,
        PlantId = project.PlantId,
        PlantName = project.Plant?.Name,
        Code = project.Code,
        Name = project.Name,
        ClientName = project.ClientName,
        Location = project.Location,
        Phase = project.Phase,
        StartDate = project.StartDate,
        EndDate = project.EndDate,
        Status = project.Status.ToString(),
        Description = project.Description,
        OwnerUserId = project.OwnerUserId,
        OwnerName = project.Owner?.DisplayName,
        PbsRootNodeId = project.PbsRootNodeId,
        PbsRootNodeName = project.PbsRootNode is null
            ? null
            : project.PbsRootNode.Code + " " + project.PbsRootNode.Name,
        CreatedAt = project.CreatedAt
    };
}
