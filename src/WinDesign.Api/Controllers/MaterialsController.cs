using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WinDesign.Api.Common;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Materials;
using WinDesign.Domain.Entities;
using WinDesign.Infrastructure;

namespace WinDesign.Api.Controllers;

/// <summary>
/// 材料/附件归项目所有，可选关联项目范围内的设备。
/// 列表与写入都按当前工作区项目范围授权。
/// </summary>
[Route("api")]
[Authorize(Policy = AuthorizationPolicies.DesignClient)]
public sealed class MaterialsController : ApiControllerBase
{
    private readonly WinDesignDbContext _db;

    public MaterialsController(WinDesignDbContext db)
    {
        _db = db;
    }

    [HttpGet("projects/{projectId:guid}/materials")]
    public async Task<ActionResult<PagedResult<MaterialItemDto>>> Get(
        Guid projectId,
        [FromQuery] string? search,
        [FromQuery] Guid? equipmentId,
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

        page = Math.Clamp(page, 1, 100_000);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = _db.MaterialItems
            .Include(x => x.Equipment)
            .Where(x => x.ProjectId == projectId)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x => x.Code.Contains(keyword) ||
                                     x.Name.Contains(keyword) ||
                                     (x.Spec != null && x.Spec.Contains(keyword)) ||
                                     (x.Material != null && x.Material.Contains(keyword)) ||
                                     (x.Remark != null && x.Remark.Contains(keyword)) ||
                                     (x.Equipment != null &&
                                      (x.Equipment.TagNo.Contains(keyword) ||
                                       x.Equipment.Name.Contains(keyword))));
        }
        if (equipmentId is Guid filterEquipmentId)
        {
            query = query.Where(x => x.EquipmentId == filterEquipmentId);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.Code)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new PagedResult<MaterialItemDto>(
            items.Select(ToDto).ToList(), total, page, pageSize));
    }

    [HttpPost("projects/{projectId:guid}/materials")]
    [Authorize(Roles = "Admin,Designer")]
    public async Task<ActionResult<MaterialItemDto>> Create(
        Guid projectId,
        [FromBody] MaterialItemUpsertRequest request,
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
        if (await ValidateRequestAsync(project, request, null, ct) is ActionResult invalid)
        {
            return invalid;
        }

        var material = new MaterialItem
        {
            ProjectId = projectId,
            EquipmentId = request.EquipmentId,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Spec = Clean(request.Spec),
            Material = Clean(request.Material),
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? "个" : request.Unit.Trim(),
            Quantity = request.Quantity,
            Remark = Clean(request.Remark)
        };
        _db.MaterialItems.Add(material);
        _db.AddAudit(CurrentUserId, CurrentUserName, "Create", "MaterialItem",
            material.Id.ToString(), material.Code);
        await _db.SaveChangesAsync(ct);

        var created = await _db.MaterialItems.Include(x => x.Equipment).AsNoTracking()
            .SingleAsync(x => x.Id == material.Id, ct);
        return Ok(ToDto(created));
    }

    [HttpPut("materials/{id:guid}")]
    [Authorize(Roles = "Admin,Designer")]
    public async Task<ActionResult<MaterialItemDto>> Update(
        Guid id,
        [FromBody] MaterialItemUpsertRequest request,
        CancellationToken ct)
    {
        var material = await _db.MaterialItems
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (material is null)
        {
            return NotFound(new ApiError("材料/附件不存在"));
        }
        if (EnsureWorkspaceProject(material.ProjectId) is ActionResult denied)
        {
            return denied;
        }
        if (!await CanAccessProjectAsync(material.ProjectId, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError("没有访问该项目的权限"));
        }

        var project = await _db.Projects.AsNoTracking()
            .SingleAsync(x => x.Id == material.ProjectId, ct);
        if (await ValidateRequestAsync(project, request, id, ct) is ActionResult invalid)
        {
            return invalid;
        }

        material.EquipmentId = request.EquipmentId;
        material.Code = request.Code.Trim();
        material.Name = request.Name.Trim();
        material.Spec = Clean(request.Spec);
        material.Material = Clean(request.Material);
        material.Unit = string.IsNullOrWhiteSpace(request.Unit) ? "个" : request.Unit.Trim();
        material.Quantity = request.Quantity;
        material.Remark = Clean(request.Remark);
        material.UpdatedAt = DateTime.UtcNow;

        _db.AddAudit(CurrentUserId, CurrentUserName, "Update", "MaterialItem",
            material.Id.ToString(), material.Code);
        await _db.SaveChangesAsync(ct);

        var updated = await _db.MaterialItems.Include(x => x.Equipment).AsNoTracking()
            .SingleAsync(x => x.Id == material.Id, ct);
        return Ok(ToDto(updated));
    }

    [HttpDelete("materials/{id:guid}")]
    [Authorize(Roles = "Admin,Designer")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var material = await _db.MaterialItems.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (material is null)
        {
            return NotFound(new ApiError("材料/附件不存在"));
        }
        if (EnsureWorkspaceProject(material.ProjectId) is ActionResult denied)
        {
            return denied;
        }
        if (!await CanAccessProjectAsync(material.ProjectId, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ApiError("没有访问该项目的权限"));
        }

        _db.MaterialItems.Remove(material);
        _db.AddAudit(CurrentUserId, CurrentUserName, "Delete", "MaterialItem",
            material.Id.ToString(), material.Code);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<ActionResult?> ValidateRequestAsync(
        Project project,
        MaterialItemUpsertRequest request,
        Guid? excludeId,
        CancellationToken ct)
    {
        var code = (request.Code ?? string.Empty).Trim();
        var name = (request.Name ?? string.Empty).Trim();
        if (code.Length == 0 || name.Length == 0)
        {
            return BadRequest(new ApiError("材料编码和名称不能为空"));
        }
        if (code.Length > 64)
        {
            return BadRequest(new ApiError("材料编码不能超过 64 个字符"));
        }
        if (name.Length > 200)
        {
            return BadRequest(new ApiError("材料名称不能超过 200 个字符"));
        }
        if (request.Quantity < 0)
        {
            return BadRequest(new ApiError("数量不能为负数"));
        }

        if (request.EquipmentId is Guid equipmentId)
        {
            var equipment = await _db.Equipments.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == equipmentId, ct);
            if (equipment is null)
            {
                return BadRequest(new ApiError("关联设备不存在"));
            }
            if (equipment.PlantId != project.PlantId)
            {
                return BadRequest(new ApiError("关联设备不属于当前项目工厂"));
            }
            var inScope = await _db.ProjectEquipments
                .AnyAsync(x => x.ProjectId == project.Id && x.EquipmentId == equipmentId, ct);
            if (!inScope)
            {
                return BadRequest(new ApiError("关联设备不在当前项目范围内"));
            }
        }

        var duplicate = await _db.MaterialItems.AnyAsync(
            x => x.ProjectId == project.Id && x.Code == code &&
                 (excludeId == null || x.Id != excludeId), ct);
        if (duplicate)
        {
            return Conflict(new ApiError("该项目下材料编码已存在"));
        }

        return null;
    }

    private async Task<bool> CanAccessProjectAsync(Guid projectId, CancellationToken ct) =>
        User.IsInRole("Admin") ||
        await _db.ProjectMembers.AnyAsync(x => x.ProjectId == projectId && x.UserId == CurrentUserId, ct);

    private static MaterialItemDto ToDto(MaterialItem material) => new()
    {
        Id = material.Id,
        ProjectId = material.ProjectId,
        EquipmentId = material.EquipmentId,
        EquipmentTagNo = material.Equipment?.TagNo,
        EquipmentName = material.Equipment?.Name,
        Code = material.Code,
        Name = material.Name,
        Spec = material.Spec,
        Material = material.Material,
        Unit = material.Unit,
        Quantity = material.Quantity,
        Remark = material.Remark,
        CreatedAt = material.CreatedAt,
        UpdatedAt = material.UpdatedAt
    };

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
