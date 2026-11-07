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
                                     (x.TypeCode != null && x.TypeCode.Contains(keyword)) ||
                                     (x.Location != null && x.Location.Contains(keyword)));
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

        return Ok(new PagedResult<EquipmentDto>(items.Select(ToDto).ToList(), total, page, pageSize));
    }

    [HttpGet("equipments/{id:guid}")]
    public async Task<ActionResult<EquipmentDto>> GetById(Guid id, CancellationToken ct)
    {
        var equipment = await _db.Equipments
            .Include(x => x.PbsNode)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        return equipment is null ? NotFound(new ApiError("设备不存在")) : Ok(ToDto(equipment));
    }

    [HttpPost("projects/{projectId:guid}/equipments")]
    [Authorize(Roles = "Admin,Designer")]
    public async Task<ActionResult<EquipmentDto>> Create(
        Guid projectId,
        [FromBody] CreateEquipmentRequest request,
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
            Name = request.Name.Trim(),
            TypeCode = string.IsNullOrWhiteSpace(request.TypeCode) ? null : request.TypeCode.Trim(),
            Discipline = discipline,
            Status = status,
            Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim()
        };
        _db.Equipments.Add(equipment);
        _db.AddAudit(CurrentUserId, CurrentUserName, "Create", "Equipment", equipment.Id.ToString(), tagNo);
        await _db.SaveChangesAsync(ct);

        var created = await _db.Equipments.Include(x => x.PbsNode).AsNoTracking()
            .SingleAsync(x => x.Id == equipment.Id, ct);
        return Ok(ToDto(created));
    }

    [HttpPut("equipments/{id:guid}")]
    [Authorize(Roles = "Admin,Designer")]
    public async Task<ActionResult<EquipmentDto>> Update(
        Guid id,
        [FromBody] UpdateEquipmentRequest request,
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
        equipment.Name = request.Name.Trim();
        equipment.TypeCode = string.IsNullOrWhiteSpace(request.TypeCode) ? null : request.TypeCode.Trim();
        equipment.Discipline = discipline;
        equipment.Status = status;
        equipment.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        equipment.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        equipment.UpdatedAt = DateTime.UtcNow;

        _db.AddAudit(CurrentUserId, CurrentUserName, "Update", "Equipment", equipment.Id.ToString(), tagNo);
        await _db.SaveChangesAsync(ct);

        var updated = await _db.Equipments.Include(x => x.PbsNode).AsNoTracking()
            .SingleAsync(x => x.Id == equipment.Id, ct);
        return Ok(ToDto(updated));
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

    private static EquipmentDto ToDto(Equipment equipment) => new(
        equipment.Id,
        equipment.ProjectId,
        equipment.PbsNodeId,
        equipment.PbsNode?.Name,
        equipment.TagNo,
        equipment.Name,
        equipment.TypeCode,
        equipment.Discipline.ToString(),
        equipment.Status.ToString(),
        equipment.Location,
        equipment.Description,
        equipment.CreatedAt);
}
