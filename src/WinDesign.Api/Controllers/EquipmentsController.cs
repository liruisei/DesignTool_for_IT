using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WinDesign.Api.Common;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Equipment;
using WinDesign.Domain;
using WinDesign.Domain.Entities;
using WinDesign.Infrastructure;

namespace WinDesign.Api.Controllers;

[Route("api")]
public sealed class EquipmentsController : ApiControllerBase
{
    private readonly WinDesignDbContext _db;

    public EquipmentsController(WinDesignDbContext db)
    {
        _db = db;
    }

    [HttpGet("projects/{projectId:guid}/equipments")]
    public async Task<ActionResult<PagedResult<EquipmentDto>>> Get(
        Guid projectId,
        [FromQuery] string? search,
        [FromQuery] string? loopNo,
        [FromQuery] string? discipline,
        [FromQuery] string? status,
        [FromQuery] Guid? pbsNodeId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (!await _db.Projects.AnyAsync(x => x.Id == projectId, ct))
        {
            return NotFound(new ApiError("项目不存在"));
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = _db.Equipments
            .Include(x => x.PbsNode)
            .Where(x => x.ProjectId == projectId)
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
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<EquipmentStatus>(status, true, out var parsedStatus))
            {
                return BadRequest(new ApiError("设备状态无效"));
            }
            query = query.Where(x => x.Status == parsedStatus);
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

        var pathMap = await BuildPbsPathMapAsync(projectId, ct);
        return Ok(new PagedResult<EquipmentDto>(
            items.Select(x => ToDto(x, pathMap)).ToList(), total, page, pageSize));
    }

    [HttpGet("equipments/{id:guid}")]
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

        var pathMap = await BuildPbsPathMapAsync(equipment.ProjectId, ct);
        return Ok(ToDto(equipment, pathMap));
    }

    [HttpPost("projects/{projectId:guid}/equipments")]
    [Authorize(Roles = "Admin,Designer")]
    public async Task<ActionResult<EquipmentDto>> Create(
        Guid projectId,
        [FromBody] EquipmentUpsertRequest request,
        CancellationToken ct)
    {
        if (!await _db.Projects.AnyAsync(x => x.Id == projectId, ct))
        {
            return NotFound(new ApiError("项目不存在"));
        }

        var tagNo = request.TagNo.Trim();
        if (string.IsNullOrWhiteSpace(tagNo) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiError("设备位号和名称不能为空"));
        }
        if (await _db.Equipments.AnyAsync(x => x.ProjectId == projectId && x.TagNo == tagNo, ct))
        {
            return Conflict(new ApiError("设备位号已存在"));
        }
        if (!Enum.TryParse<Discipline>(request.Discipline, true, out var discipline))
        {
            return BadRequest(new ApiError("专业无效"));
        }
        var status = EquipmentStatus.Planned;
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            !Enum.TryParse(request.Status, true, out status))
        {
            return BadRequest(new ApiError("设备状态无效"));
        }
        if (request.PbsNodeId is Guid pbsNodeId)
        {
            var valid = await _db.PbsNodes
                .AnyAsync(x => x.Id == pbsNodeId && x.ProjectId == projectId, ct);
            if (!valid)
            {
                return BadRequest(new ApiError("PBS 节点不存在或不属于当前项目"));
            }
        }

        var equipment = new Equipment
        {
            ProjectId = projectId,
            PbsNodeId = request.PbsNodeId,
            TagNo = tagNo,
            LoopNo = Clean(request.LoopNo),
            Name = request.Name.Trim(),
            TypeCode = Clean(request.TypeCode),
            Discipline = discipline,
            Status = status,
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
        _db.AddAudit(CurrentUserId, CurrentUserName, "Create", "Equipment", equipment.Id.ToString(), tagNo);
        await _db.SaveChangesAsync(ct);

        var created = await _db.Equipments.Include(x => x.PbsNode).AsNoTracking()
            .SingleAsync(x => x.Id == equipment.Id, ct);
        var createdPaths = await BuildPbsPathMapAsync(projectId, ct);
        return Ok(ToDto(created, createdPaths));
    }

    [HttpPut("equipments/{id:guid}")]
    [Authorize(Roles = "Admin,Designer")]
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

        var tagNo = request.TagNo.Trim();
        if (string.IsNullOrWhiteSpace(tagNo) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiError("设备位号和名称不能为空"));
        }
        if (await _db.Equipments.AnyAsync(x => x.ProjectId == equipment.ProjectId && x.TagNo == tagNo && x.Id != id, ct))
        {
            return Conflict(new ApiError("设备位号已存在"));
        }
        if (!Enum.TryParse<Discipline>(request.Discipline, true, out var discipline))
        {
            return BadRequest(new ApiError("专业无效"));
        }
        var status = equipment.Status;
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            !Enum.TryParse(request.Status, true, out status))
        {
            return BadRequest(new ApiError("设备状态无效"));
        }
        if (request.PbsNodeId is Guid pbsNodeId)
        {
            var valid = await _db.PbsNodes
                .AnyAsync(x => x.Id == pbsNodeId && x.ProjectId == equipment.ProjectId, ct);
            if (!valid)
            {
                return BadRequest(new ApiError("PBS 节点不存在或不属于当前项目"));
            }
        }

        equipment.PbsNodeId = request.PbsNodeId;
        equipment.TagNo = tagNo;
        equipment.LoopNo = Clean(request.LoopNo);
        equipment.Name = request.Name.Trim();
        equipment.TypeCode = Clean(request.TypeCode);
        equipment.Discipline = discipline;
        equipment.Status = status;
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
        var updatedPaths = await BuildPbsPathMapAsync(equipment.ProjectId, ct);
        return Ok(ToDto(updated, updatedPaths));
    }

    [HttpDelete("equipments/{id:guid}")]
    [Authorize(Roles = "Admin,Designer")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var equipment = await _db.Equipments.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (equipment is null)
        {
            return NotFound(new ApiError("设备不存在"));
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

    private async Task<Dictionary<Guid, string>> BuildPbsPathMapAsync(
        Guid projectId,
        CancellationToken ct)
    {
        var nodes = await _db.PbsNodes
            .Where(x => x.ProjectId == projectId)
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
        IReadOnlyDictionary<Guid, string>? pbsPaths = null) => new()
    {
        Id = equipment.Id,
        ProjectId = equipment.ProjectId,
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
        Status = equipment.Status.ToString(),
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
