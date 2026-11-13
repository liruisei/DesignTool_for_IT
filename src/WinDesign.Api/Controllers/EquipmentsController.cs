using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WinDesign.Api.Common;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Permissions;
using WinDesign.Domain;
using WinDesign.Domain.Entities;
using WinDesign.Infrastructure;

namespace WinDesign.Api.Controllers;

/// <summary>
/// 设备归工厂所有，项目通过 project_equipments 决定设计范围。
/// 索引表按当前工作区项目过滤；PBS 管理按当前工作区工厂校验。
/// </summary>
[Route("api")]
[Authorize(Policy = AuthorizationPolicies.DesignClient)]
public sealed class EquipmentsController : ApiControllerBase
{
    private readonly WinDesignDbContext _db;

    public EquipmentsController(WinDesignDbContext db)
    {
        _db = db;
    }

    [HttpGet("projects/{projectId:guid}/equipments")]
    [RequirePermission("equipment.read")]
    public async Task<ActionResult<PagedResult<EquipmentDto>>> Get(
        Guid projectId,
        [FromQuery] string? search,
        [FromQuery] string? loopNo,
        [FromQuery] string? discipline,
        [FromQuery] Guid? pbsNodeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (EnsureWorkspaceProject(projectId) is ActionResult denied)
        {
            return denied;
        }

        var project = await _db.Projects.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == projectId, ct);
        if (project is null)
        {
            return NotFound(new ApiError("项目不存在"));
        }
        if (!await CanAccessProjectAsync(projectId, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError("没有访问该项目的权限"));
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = _db.Equipments
            .Include(x => x.PbsNode)
            .Where(x => x.PlantId == project.PlantId &&
                        x.ProjectEquipments.Any(link => link.ProjectId == projectId))
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x => x.TagNo.Contains(keyword) || x.Name.Contains(keyword) ||
                                     (x.LoopNo != null && x.LoopNo.Contains(keyword)) ||
                                     (x.TypeCode != null && x.TypeCode.Contains(keyword)) ||
                                     (x.PidDrawingNo != null && x.PidDrawingNo.Contains(keyword)) ||
                                     (x.SpecNo != null && x.SpecNo.Contains(keyword)) ||
                                     (x.PipeEquipmentNo != null && x.PipeEquipmentNo.Contains(keyword)) ||
                                     (x.Location != null && x.Location.Contains(keyword)));
        }
        if (!string.IsNullOrWhiteSpace(loopNo))
        {
            var loop = loopNo.Trim();
            query = query.Where(x => x.LoopNo == loop);
        }
        if (!string.IsNullOrWhiteSpace(discipline))
        {
            if (!Enum.TryParse<Discipline>(discipline, true, out var parsedDiscipline))
            {
                return BadRequest(new ApiError("专业无效"));
            }
            query = query.Where(x => x.Discipline == parsedDiscipline);
        }
        if (pbsNodeId is Guid nodeId)
        {
            query = query.Where(x => x.PbsNodeId == nodeId);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.TagNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var pathMap = await BuildPbsPathMapAsync(project.PlantId, ct);
        return Ok(new PagedResult<EquipmentDto>(
            items.Select(x => ToDto(x, projectId, pathMap)).ToList(), total, page, pageSize));
    }

    [HttpGet("equipments/{id:guid}")]
    [RequirePermission("equipment.read")]
    public async Task<ActionResult<EquipmentDto>> GetById(Guid id, CancellationToken ct)
    {
        var equipment = await _db.Equipments
            .Include(x => x.PbsNode)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (equipment is null)
        {
            return NotFound(new ApiError("设备不存在"));
        }
        if (CurrentPlantId != equipment.PlantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("只能访问登录工作区中选定的工厂"));
        }
        if (!User.IsInRole("Admin") &&
            CurrentProjectId is Guid projectId &&
            !await _db.ProjectEquipments.AnyAsync(x => x.ProjectId == projectId && x.EquipmentId == id, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("只能访问当前项目范围内的设备"));
        }

        var pathMap = await BuildPbsPathMapAsync(equipment.PlantId, ct);
        return Ok(ToDto(equipment, CurrentProjectId, pathMap));
    }

    [HttpPost("projects/{projectId:guid}/equipments")]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("equipment.create")]
    public async Task<ActionResult<EquipmentDto>> Create(
        Guid projectId,
        [FromBody] EquipmentUpsertRequest request,
        CancellationToken ct)
    {
        if (EnsureWorkspaceProject(projectId) is ActionResult denied)
        {
            return denied;
        }

        var project = await _db.Projects.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == projectId, ct);
        if (project is null)
        {
            return NotFound(new ApiError("项目不存在"));
        }
        if (!await CanAccessProjectAsync(projectId, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError("没有访问该项目的权限"));
        }

        // 位号按大写保存并做不区分大小写的唯一性校验，避免复制时大小写变体绕过唯一索引。
        var tagNo = request.TagNo.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(tagNo) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiError("设备位号和名称不能为空"));
        }
        if (await _db.Equipments.AnyAsync(
                x => x.PlantId == project.PlantId && x.TagNo.ToUpper() == tagNo, ct))
        {
            return Conflict(new ApiError("该工厂下设备位号已存在"));
        }
        if (!Enum.TryParse<Discipline>(request.Discipline, true, out var discipline))
        {
            return BadRequest(new ApiError("专业无效"));
        }
        if (request.PbsNodeId is Guid pbsNodeId)
        {
            var valid = await _db.PbsNodes
                .AnyAsync(x => x.Id == pbsNodeId && x.PlantId == project.PlantId, ct);
            if (!valid)
            {
                return BadRequest(new ApiError("PBS 节点不存在或不属于当前工厂"));
            }
        }

        var equipment = new Equipment
        {
            PlantId = project.PlantId,
            PbsNodeId = request.PbsNodeId,
            TagNo = tagNo,
            LoopNo = Clean(request.LoopNo),
            Name = request.Name.Trim(),
            TypeCode = Clean(request.TypeCode),
            Discipline = discipline,
            Location = Clean(request.Location),
            PipeEquipmentNo = Clean(request.PipeEquipmentNo),
            Quantity = request.Quantity > 0 ? request.Quantity : 1,
            Purpose = Clean(request.Purpose),
            PidDrawingNo = Clean(request.PidDrawingNo),
            SpecNo = Clean(request.SpecNo),
            SpecPageNo = Clean(request.SpecPageNo),
            LoopDrawingNo = Clean(request.LoopDrawingNo),
            InstallDrawingNo = Clean(request.InstallDrawingNo),
            ElectricalDrawingNo = Clean(request.ElectricalDrawingNo),
            HeatSupplyDrawingNo = Clean(request.HeatSupplyDrawingNo),
            AirSupplyDrawingNo = Clean(request.AirSupplyDrawingNo),
            LayingDrawingNo = Clean(request.LayingDrawingNo),
            JunctionBoxDrawingNo = Clean(request.JunctionBoxDrawingNo),
            Keyword = Clean(request.Keyword),
            IoSetting = Clean(request.IoSetting),
            IndexNo = Clean(request.IndexNo),
            SortOrder = request.SortOrder,
            Description = Clean(request.Description),
            ProcessCondition = Clean(request.ProcessCondition),
            Specification = Clean(request.Specification)
        };
        _db.Equipments.Add(equipment);
        // 在当前项目下创建，设备自动纳入该项目范围。
        _db.ProjectEquipments.Add(new ProjectEquipment
        {
            ProjectId = projectId,
            EquipmentId = equipment.Id
        });
        _db.AddAudit(CurrentUserId, CurrentUserName, "Create", "Equipment", equipment.Id.ToString(), tagNo);
        await _db.SaveChangesAsync(ct);

        var created = await _db.Equipments.Include(x => x.PbsNode).AsNoTracking()
            .SingleAsync(x => x.Id == equipment.Id, ct);
        var createdPaths = await BuildPbsPathMapAsync(project.PlantId, ct);
        return Ok(ToDto(created, projectId, createdPaths));
    }

    [HttpPut("equipments/{id:guid}")]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("equipment.update")]
    public async Task<ActionResult<EquipmentDto>> Update(
        Guid id,
        [FromBody] EquipmentUpsertRequest request,
        CancellationToken ct)
    {
        var equipment = await _db.Equipments.Include(x => x.PbsNode)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (equipment is null)
        {
            return NotFound(new ApiError("设备不存在"));
        }
        if (CurrentPlantId != equipment.PlantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("只能访问登录工作区中选定的工厂"));
        }

        var projectId = CurrentProjectId;
        if (!User.IsInRole("Admin") && projectId is Guid currentProjectId)
        {
            var inScope = await _db.ProjectEquipments
                .AnyAsync(x => x.ProjectId == currentProjectId && x.EquipmentId == id, ct);
            if (!inScope)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new ApiError("只能修改当前项目范围内的设备"));
            }
        }

        // 位号按大写保存并做不区分大小写的唯一性校验。
        var tagNo = request.TagNo.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(tagNo) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiError("设备位号和名称不能为空"));
        }
        if (await _db.Equipments.AnyAsync(
                x => x.PlantId == equipment.PlantId && x.TagNo.ToUpper() == tagNo && x.Id != id, ct))
        {
            return Conflict(new ApiError("该工厂下设备位号已存在"));
        }
        if (!Enum.TryParse<Discipline>(request.Discipline, true, out var discipline))
        {
            return BadRequest(new ApiError("专业无效"));
        }
        if (request.PbsNodeId is Guid pbsNodeId)
        {
            var valid = await _db.PbsNodes
                .AnyAsync(x => x.Id == pbsNodeId && x.PlantId == equipment.PlantId, ct);
            if (!valid)
            {
                return BadRequest(new ApiError("PBS 节点不存在或不属于当前工厂"));
            }
        }

        equipment.PbsNodeId = request.PbsNodeId;
        equipment.TagNo = tagNo;
        equipment.LoopNo = Clean(request.LoopNo);
        equipment.Name = request.Name.Trim();
        equipment.TypeCode = Clean(request.TypeCode);
        equipment.Discipline = discipline;
        equipment.Location = Clean(request.Location);
        equipment.PipeEquipmentNo = Clean(request.PipeEquipmentNo);
        equipment.Quantity = request.Quantity > 0 ? request.Quantity : 1;
        equipment.Purpose = Clean(request.Purpose);
        equipment.PidDrawingNo = Clean(request.PidDrawingNo);
        equipment.SpecNo = Clean(request.SpecNo);
        equipment.SpecPageNo = Clean(request.SpecPageNo);
        equipment.LoopDrawingNo = Clean(request.LoopDrawingNo);
        equipment.InstallDrawingNo = Clean(request.InstallDrawingNo);
        equipment.ElectricalDrawingNo = Clean(request.ElectricalDrawingNo);
        equipment.HeatSupplyDrawingNo = Clean(request.HeatSupplyDrawingNo);
        equipment.AirSupplyDrawingNo = Clean(request.AirSupplyDrawingNo);
        equipment.LayingDrawingNo = Clean(request.LayingDrawingNo);
        equipment.JunctionBoxDrawingNo = Clean(request.JunctionBoxDrawingNo);
        equipment.Keyword = Clean(request.Keyword);
        equipment.IoSetting = Clean(request.IoSetting);
        equipment.IndexNo = Clean(request.IndexNo);
        equipment.SortOrder = request.SortOrder;
        equipment.Description = Clean(request.Description);
        equipment.ProcessCondition = Clean(request.ProcessCondition);
        equipment.Specification = Clean(request.Specification);
        equipment.UpdatedAt = DateTime.UtcNow;

        _db.AddAudit(CurrentUserId, CurrentUserName, "Update", "Equipment", equipment.Id.ToString(), tagNo);
        await _db.SaveChangesAsync(ct);

        var updated = await _db.Equipments.Include(x => x.PbsNode).AsNoTracking()
            .SingleAsync(x => x.Id == equipment.Id, ct);
        var updatedPaths = await BuildPbsPathMapAsync(equipment.PlantId, ct);
        return Ok(ToDto(updated, projectId, updatedPaths));
    }

    [HttpDelete("equipments/{id:guid}")]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("equipment.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var equipment = await _db.Equipments.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (equipment is null)
        {
            return NotFound(new ApiError("设备不存在"));
        }
        if (CurrentPlantId != equipment.PlantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("只能访问登录工作区中选定的工厂"));
        }
        if (!User.IsInRole("Admin") && CurrentProjectId is Guid projectId)
        {
            var inScope = await _db.ProjectEquipments
                .AnyAsync(x => x.ProjectId == projectId && x.EquipmentId == id, ct);
            if (!inScope)
            {
                return StatusCode(StatusCodes.Status403Forbidden,
                    new ApiError("只能删除当前项目范围内的设备"));
            }
        }
        if (await _db.EquipmentSpecs.AnyAsync(x => x.EquipmentId == id, ct))
        {
            return BadRequest(new ApiError("设备下存在规格书，不能删除"));
        }
        if (await _db.MaterialItems.AnyAsync(x => x.EquipmentId == id, ct))
        {
            return BadRequest(new ApiError("设备下存在材料/附件，不能删除"));
        }

        _db.Equipments.Remove(equipment);
        _db.AddAudit(CurrentUserId, CurrentUserName, "Delete", "Equipment", equipment.Id.ToString(), equipment.TagNo);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<bool> CanAccessProjectAsync(Guid projectId, CancellationToken ct) =>
        User.IsInRole("Admin") ||
        await _db.ProjectMembers.AnyAsync(x => x.ProjectId == projectId && x.UserId == CurrentUserId, ct);

    private async Task<Dictionary<Guid, string>> BuildPbsPathMapAsync(
        Guid plantId,
        CancellationToken ct)
    {
        var nodes = await _db.PbsNodes
            .Where(x => x.PlantId == plantId)
            .AsNoTracking()
            .ToListAsync(ct);
        var byId = nodes.ToDictionary(x => x.Id);
        var cache = new Dictionary<Guid, string>();

        string Build(PbsNode node)
        {
            if (cache.TryGetValue(node.Id, out var cached))
            {
                return cached;
            }

            var parts = new List<string>();
            var current = node;
            while (current is not null)
            {
                parts.Add(current.Code + " " + current.Name);
                current = current.ParentId is Guid parentId && byId.TryGetValue(parentId, out var parent)
                    ? parent
                    : null!;
            }

            parts.Reverse();
            var path = string.Join(" / ", parts);
            cache[node.Id] = path;
            return path;
        }

        return nodes.ToDictionary(x => x.Id, x => Build(x));
    }

    private static EquipmentDto ToDto(
        Equipment equipment,
        Guid? currentProjectId,
        IReadOnlyDictionary<Guid, string>? pbsPaths = null) => new()
    {
        Id = equipment.Id,
        PlantId = equipment.PlantId,
        ProjectId = currentProjectId ?? Guid.Empty,
        PbsNodeId = equipment.PbsNodeId,
        PbsNodeName = equipment.PbsNode?.Name,
        PbsPath = equipment.PbsNodeId is Guid pbsNodeId && pbsPaths is not null &&
                  pbsPaths.TryGetValue(pbsNodeId, out var pbsPath)
            ? pbsPath
            : null,
        TagNo = equipment.TagNo,
        LoopNo = equipment.LoopNo,
        Name = equipment.Name,
        TypeCode = equipment.TypeCode,
        Discipline = equipment.Discipline.ToString(),
        Location = equipment.Location,
        PipeEquipmentNo = equipment.PipeEquipmentNo,
        Quantity = equipment.Quantity,
        Purpose = equipment.Purpose,
        PidDrawingNo = equipment.PidDrawingNo,
        SpecNo = equipment.SpecNo,
        SpecPageNo = equipment.SpecPageNo,
        LoopDrawingNo = equipment.LoopDrawingNo,
        InstallDrawingNo = equipment.InstallDrawingNo,
        ElectricalDrawingNo = equipment.ElectricalDrawingNo,
        HeatSupplyDrawingNo = equipment.HeatSupplyDrawingNo,
        AirSupplyDrawingNo = equipment.AirSupplyDrawingNo,
        LayingDrawingNo = equipment.LayingDrawingNo,
        JunctionBoxDrawingNo = equipment.JunctionBoxDrawingNo,
        Keyword = equipment.Keyword,
        IoSetting = equipment.IoSetting,
        IndexNo = equipment.IndexNo,
        SortOrder = equipment.SortOrder,
        Description = equipment.Description,
        ProcessCondition = equipment.ProcessCondition,
        Specification = equipment.Specification,
        CreatedAt = equipment.CreatedAt
    };

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
