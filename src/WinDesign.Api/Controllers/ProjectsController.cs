using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WinDesign.Api.Common;
using WinDesign.Contracts.Common;
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

    [HttpPost]
    [Authorize(Roles = "Admin,Designer")]
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
            RoleInProject = "Owner"
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
