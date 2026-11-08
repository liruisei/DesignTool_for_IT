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
        if (CurrentPlantId is not Guid plantId)
        {
            return BadRequest(new ApiError("请先选择工作区工厂"));
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var userId = CurrentUserId;
        var isAdmin = User.IsInRole("Admin");

        var query = _db.Projects
            .Include(x => x.Owner)
            .Include(x => x.Plant)
            .Where(x => x.PlantId == plantId)
            .AsNoTracking();

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
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct);

        if (project is null || project.PlantId != CurrentPlantId)
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
        if (CurrentPlantId is not Guid plantId)
        {
            return BadRequest(new ApiError("请先选择工作区工厂"));
        }

        var code = request.Code.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiError("项目代号和项目名称不能为空"));
        }
        if (await _db.Projects.AnyAsync(x => x.PlantId == plantId && x.Code == code, ct))
        {
            return Conflict(new ApiError("该工厂下项目代号已存在"));
        }

        var project = new Project
        {
            PlantId = plantId,
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
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (project is null || project.PlantId != CurrentPlantId)
        {
            return NotFound(new ApiError("项目不存在"));
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
        return Ok(ToDto(project));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var project = await _db.Projects.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (project is null || project.PlantId != CurrentPlantId)
        {
            return NotFound(new ApiError("项目不存在"));
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
        CreatedAt = project.CreatedAt
    };
}
