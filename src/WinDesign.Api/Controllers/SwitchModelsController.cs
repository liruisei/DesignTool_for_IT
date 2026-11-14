using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WinDesign.Api.Common;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Telecom;
using WinDesign.Domain;
using WinDesign.Domain.Entities;
using WinDesign.Infrastructure;

namespace WinDesign.Api.Controllers;

/// <summary>
/// 交换机样本库。管理端维护、设计端只读，电信统计按样本的端口规格计算台数。
/// </summary>
[Route("api/switch-models")]
[Authorize(Policy = AuthorizationPolicies.ClientAccess)]
public sealed class SwitchModelsController : ApiControllerBase
{
    private readonly WinDesignDbContext _db;

    public SwitchModelsController(WinDesignDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [RequirePermission("switches.read")]
    public async Task<ActionResult<IReadOnlyList<SwitchModelDto>>> Get(
        [FromQuery] bool activeOnly = false,
        CancellationToken ct = default)
    {
        var query = _db.SwitchModels.AsNoTracking();
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        var models = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .ToListAsync(ct);
        return Ok(models.Select(ToDto).ToList());
    }

    [HttpPost]
    [RequirePermission("switches.create")]
    public async Task<ActionResult<SwitchModelDto>> Create(
        [FromBody] SwitchModelUpsertRequest request,
        CancellationToken ct)
    {
        var validation = await ValidateAsync(request, null, ct);
        if (validation is ActionResult error)
        {
            return error;
        }

        var model = new SwitchModel
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            SwitchType = Enum.Parse<SwitchType>(request.SwitchType, true),
            ElectricalPorts = request.ElectricalPorts,
            UplinkPorts = request.UplinkPorts,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
            Remark = Clean(request.Remark)
        };
        _db.SwitchModels.Add(model);
        _db.AddAudit(CurrentUserId, CurrentUserName, "Create", "SwitchModel",
            model.Id.ToString(), model.Code);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(model));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("switches.update")]
    public async Task<ActionResult<SwitchModelDto>> Update(
        Guid id,
        [FromBody] SwitchModelUpsertRequest request,
        CancellationToken ct)
    {
        var model = await _db.SwitchModels.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (model is null)
        {
            return NotFound(new ApiError("设备样本不存在"));
        }

        var validation = await ValidateAsync(request, id, ct);
        if (validation is ActionResult error)
        {
            return error;
        }

        model.Code = request.Code.Trim();
        model.Name = request.Name.Trim();
        model.SwitchType = Enum.Parse<SwitchType>(request.SwitchType, true);
        model.ElectricalPorts = request.ElectricalPorts;
        model.UplinkPorts = request.UplinkPorts;
        model.IsActive = request.IsActive;
        model.SortOrder = request.SortOrder;
        model.Remark = Clean(request.Remark);
        model.UpdatedAt = DateTime.UtcNow;

        _db.AddAudit(CurrentUserId, CurrentUserName, "Update", "SwitchModel",
            model.Id.ToString(), model.Code);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(model));
    }

    /// <summary>列表勾选启用/停用后批量保存，逐行调用；与用户管理的启用逻辑一致。</summary>
    [HttpPost("{id:guid}/toggle-active")]
    [RequirePermission("switches.update")]
    public async Task<ActionResult<SwitchModelDto>> ToggleActive(Guid id, CancellationToken ct)
    {
        var model = await _db.SwitchModels.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (model is null)
        {
            return NotFound(new ApiError("设备样本不存在"));
        }

        model.IsActive = !model.IsActive;
        model.UpdatedAt = DateTime.UtcNow;
        _db.AddAudit(CurrentUserId, CurrentUserName, "Update", "SwitchModel",
            model.Id.ToString(), model.IsActive ? "启用 " + model.Code : "停用 " + model.Code);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(model));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("switches.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var model = await _db.SwitchModels.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (model is null)
        {
            return NotFound(new ApiError("设备样本不存在"));
        }

        _db.SwitchModels.Remove(model);
        _db.AddAudit(CurrentUserId, CurrentUserName, "Delete", "SwitchModel",
            model.Id.ToString(), model.Code);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<ActionResult?> ValidateAsync(
        SwitchModelUpsertRequest request,
        Guid? excludeId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiError("设备编码和名称不能为空"));
        }
        if (!Enum.TryParse<SwitchType>(request.SwitchType, true, out var deviceType))
        {
            return BadRequest(new ApiError("设备类型无效"));
        }
        if (request.ElectricalPorts < 0 || request.ElectricalPorts > 1000)
        {
            return BadRequest(new ApiError("端口数量必须在 0 到 1000 之间"));
        }
        if (deviceType is SwitchType.Access or SwitchType.PoE && request.ElectricalPorts <= 0)
        {
            return BadRequest(new ApiError("交换机类型的端口数量必须大于 0"));
        }
        if (request.UplinkPorts is < 0 or > 1000)
        {
            return BadRequest(new ApiError("上联口数量无效"));
        }

        var code = request.Code.Trim();
        var exists = await _db.SwitchModels.AnyAsync(
            x => x.Code == code && (excludeId == null || x.Id != excludeId), ct);
        if (exists)
        {
            return Conflict(new ApiError("设备编码已存在"));
        }

        return null;
    }

    private static SwitchModelDto ToDto(SwitchModel model) => new(
        model.Id,
        model.Code,
        model.Name,
        model.SwitchType.ToString(),
        model.ElectricalPorts,
        model.UplinkPorts,
        model.IsActive,
        model.SortOrder,
        model.Remark);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
