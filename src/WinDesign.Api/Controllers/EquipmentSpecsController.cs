using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WinDesign.Api.Common;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Permissions;
using WinDesign.Domain.Entities;
using WinDesign.Infrastructure;

namespace WinDesign.Api.Controllers;

/// <summary>
/// 规格书挂在设备下，设备归工厂、通过 project_equipments 纳入项目范围。
/// 列表按当前工作区项目过滤；单条读写先校验工厂，再校验项目范围。
/// </summary>
[Route("api")]
[Authorize(Policy = AuthorizationPolicies.DesignClient)]
public sealed class EquipmentSpecsController : ApiControllerBase
{
    private readonly WinDesignDbContext _db;

    public EquipmentSpecsController(WinDesignDbContext db)
    {
        _db = db;
    }

    [HttpGet("projects/{projectId:guid}/specs")]
    [RequirePermission("specs.read")]
    public async Task<ActionResult<PagedResult<EquipmentSpecDto>>> Get(
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

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = _db.EquipmentSpecs
            .Include(x => x.Equipment)
            .Where(x => x.Equipment.PlantId == project.PlantId &&
                        x.Equipment.ProjectEquipments.Any(link => link.ProjectId == projectId))
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x => x.SpecCode.Contains(keyword) ||
                                     x.SpecName.Contains(keyword) ||
                                     x.Equipment.TagNo.Contains(keyword) ||
                                     x.Equipment.Name.Contains(keyword));
        }
        if (equipmentId is Guid filterEquipmentId)
        {
            query = query.Where(x => x.EquipmentId == filterEquipmentId);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.Equipment.TagNo)
            .ThenBy(x => x.SpecCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new PagedResult<EquipmentSpecDto>(
            items.Select(x => ToDto(x)).ToList(), total, page, pageSize));
    }

    [HttpPost("equipments/{equipmentId:guid}/specs")]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("specs.create")]
    public async Task<ActionResult<EquipmentSpecDto>> Create(
        Guid equipmentId,
        [FromBody] EquipmentSpecUpsertRequest request,
        CancellationToken ct)
    {
        var equipment = await _db.Equipments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == equipmentId, ct);
        if (equipment is null)
        {
            return NotFound(new ApiError("设备不存在"));
        }
        if (CurrentPlantId != equipment.PlantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("只能访问登录工作区中选定的工厂"));
        }
        if (!User.IsInRole("Admin") && CurrentProjectId is Guid projectId &&
            !await _db.ProjectEquipments.AnyAsync(x => x.ProjectId == projectId && x.EquipmentId == equipmentId, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("只能维护当前项目范围内的设备规格书"));
        }

        if (ValidateRequest(request, out var code, out var name, out var contentJson) is ActionResult invalid)
        {
            return invalid;
        }
        if (await _db.EquipmentSpecs.AnyAsync(x => x.EquipmentId == equipmentId && x.SpecCode == code, ct))
        {
            return Conflict(new ApiError("该设备下规格书编号已存在"));
        }

        var spec = new EquipmentSpec
        {
            EquipmentId = equipmentId,
            SpecCode = code,
            SpecName = name,
            ContentJson = contentJson,
            Version = 1
        };
        _db.EquipmentSpecs.Add(spec);
        _db.AddAudit(CurrentUserId, CurrentUserName, "Create", "EquipmentSpec",
            spec.Id.ToString(), $"{equipment.TagNo}/{code}");
        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(spec, equipment));
    }

    [HttpPut("specs/{id:guid}")]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("specs.update")]
    public async Task<ActionResult<EquipmentSpecDto>> Update(
        Guid id,
        [FromBody] EquipmentSpecUpsertRequest request,
        CancellationToken ct)
    {
        var spec = await _db.EquipmentSpecs.Include(x => x.Equipment)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (spec is null)
        {
            return NotFound(new ApiError("规格书不存在"));
        }
        if (CurrentPlantId != spec.Equipment.PlantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("只能访问登录工作区中选定的工厂"));
        }
        if (!User.IsInRole("Admin") && CurrentProjectId is Guid projectId &&
            !await _db.ProjectEquipments.AnyAsync(x => x.ProjectId == projectId && x.EquipmentId == spec.EquipmentId, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("只能修改当前项目范围内的设备规格书"));
        }

        if (ValidateRequest(request, out var code, out var name, out var contentJson) is ActionResult invalid)
        {
            return invalid;
        }
        if (await _db.EquipmentSpecs.AnyAsync(
                x => x.EquipmentId == spec.EquipmentId && x.SpecCode == code && x.Id != id, ct))
        {
            return Conflict(new ApiError("该设备下规格书编号已存在"));
        }

        spec.SpecCode = code;
        spec.SpecName = name;
        spec.ContentJson = contentJson;
        spec.UpdatedAt = DateTime.UtcNow;

        _db.AddAudit(CurrentUserId, CurrentUserName, "Update", "EquipmentSpec",
            spec.Id.ToString(), $"{spec.Equipment.TagNo}/{code}");
        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(spec));
    }

    [HttpDelete("specs/{id:guid}")]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("specs.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var spec = await _db.EquipmentSpecs.Include(x => x.Equipment)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (spec is null)
        {
            return NotFound(new ApiError("规格书不存在"));
        }
        if (CurrentPlantId != spec.Equipment.PlantId)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("只能访问登录工作区中选定的工厂"));
        }
        if (!User.IsInRole("Admin") && CurrentProjectId is Guid projectId &&
            !await _db.ProjectEquipments.AnyAsync(x => x.ProjectId == projectId && x.EquipmentId == spec.EquipmentId, ct))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("只能删除当前项目范围内的设备规格书"));
        }

        _db.EquipmentSpecs.Remove(spec);
        _db.AddAudit(CurrentUserId, CurrentUserName, "Delete", "EquipmentSpec",
            spec.Id.ToString(), $"{spec.Equipment.TagNo}/{spec.SpecCode}");
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private ActionResult? ValidateRequest(
        EquipmentSpecUpsertRequest request,
        out string code,
        out string name,
        out string contentJson)
    {
        code = (request.SpecCode ?? string.Empty).Trim();
        name = (request.SpecName ?? string.Empty).Trim();
        contentJson = "{}";

        if (code.Length == 0 || name.Length == 0)
        {
            return BadRequest(new ApiError("规格书编号和名称不能为空"));
        }
        if (code.Length > 64)
        {
            return BadRequest(new ApiError("规格书编号不能超过 64 个字符"));
        }
        if (name.Length > 200)
        {
            return BadRequest(new ApiError("规格书名称不能超过 200 个字符"));
        }
        if (!SpecContentCodec.TryNormalize(request.Parameters, out var parameters, out var error))
        {
            return BadRequest(new ApiError(error ?? "规格书参数无效"));
        }

        contentJson = SpecContentCodec.Serialize(parameters);
        return null;
    }

    private async Task<bool> CanAccessProjectAsync(Guid projectId, CancellationToken ct) =>
        User.IsInRole("Admin") ||
        await _db.ProjectMembers.AnyAsync(x => x.ProjectId == projectId && x.UserId == CurrentUserId, ct);

    private static EquipmentSpecDto ToDto(EquipmentSpec spec, Equipment? equipment = null)
    {
        var parameters = SpecContentCodec.Parse(spec.ContentJson, out var warning);
        var parent = equipment ?? spec.Equipment;
        return new EquipmentSpecDto
        {
            Id = spec.Id,
            EquipmentId = spec.EquipmentId,
            EquipmentTagNo = parent?.TagNo ?? string.Empty,
            EquipmentName = parent?.Name ?? string.Empty,
            SpecCode = spec.SpecCode,
            SpecName = spec.SpecName,
            Parameters = parameters,
            Version = spec.Version,
            ContentWarning = warning,
            CreatedAt = spec.CreatedAt,
            UpdatedAt = spec.UpdatedAt
        };
    }
}
