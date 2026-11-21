using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WinDesign.Api.Common;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Dictionaries;
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

        var typeNames = await GetEquipmentTypeNamesAsync(ct);
        return Ok(models
            .Select(x => ToDto(x, typeNames.GetValueOrDefault(x.TypeCode ?? string.Empty)))
            .ToList());
    }

    [HttpPost]
    [RequirePermission("switches.create")]
    public async Task<ActionResult<SwitchModelDto>> Create(
        [FromBody] SwitchModelUpsertRequest request,
        CancellationToken ct)
    {
        var validation = await ValidateAsync(request, null, ct);
        if (validation.Error is not null)
        {
            return validation.Error;
        }

        var type = validation.Type!;
        var model = new SwitchModel
        {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Discipline = Enum.Parse<Discipline>(request.Discipline!, true).ToString(),
            TypeCode = type.Code,
            SwitchType = SwitchType.Other,
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
        return Ok(ToDto(model, type.Name));
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
        if (validation.Error is not null)
        {
            return validation.Error;
        }

        var type = validation.Type!;
        model.Code = request.Code.Trim();
        model.Name = request.Name.Trim();
        model.Discipline = Enum.Parse<Discipline>(request.Discipline!, true).ToString();
        model.TypeCode = type.Code;
        model.ElectricalPorts = request.ElectricalPorts;
        model.UplinkPorts = request.UplinkPorts;
        model.IsActive = request.IsActive;
        model.SortOrder = request.SortOrder;
        model.Remark = Clean(request.Remark);
        model.UpdatedAt = DateTime.UtcNow;

        _db.AddAudit(CurrentUserId, CurrentUserName, "Update", "SwitchModel",
            model.Id.ToString(), model.Code);
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(model, type.Name));
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
        return Ok(ToDto(model, await GetEquipmentTypeNameAsync(model.TypeCode, ct)));
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

    private async Task<(DictionaryItem? Type, ActionResult? Error)> ValidateAsync(
        SwitchModelUpsertRequest request,
        Guid? excludeId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return (null, BadRequest(new ApiError("设备编码和名称不能为空")));
        }
        if (!Enum.TryParse<Discipline>(request.Discipline, true, out var discipline))
        {
            return (null, BadRequest(new ApiError("请选择专业")));
        }
        if (string.IsNullOrWhiteSpace(request.TypeCode))
        {
            return (null, BadRequest(new ApiError("请选择设备类型")));
        }

        var typeCode = request.TypeCode.Trim();
        var type = await _db.DictionaryItems.AsNoTracking()
            .Include(x => x.Type)
            .FirstOrDefaultAsync(
                x => x.Type.Code == "EQUIPMENT_TYPE" && x.Code == typeCode, ct);
        if (type is null)
        {
            return (null, BadRequest(new ApiError("设备类型不存在")));
        }

        // 设备类型配置了专业时，样本必须与类型定义的专业一致。
        var typeDiscipline = DictionaryItemExtras.GetDiscipline(type.ExtraJson);
        if (typeDiscipline is not null &&
            !string.Equals(typeDiscipline, discipline.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return (null, BadRequest(new ApiError("所选设备类型不属于该专业")));
        }

        if (request.ElectricalPorts < 0 || request.ElectricalPorts > 1000)
        {
            return (null, BadRequest(new ApiError("端口数量必须在 0 到 1000 之间")));
        }

        // 交换机类设备必须有电口；机柜等其他样本允许 0 口。
        if (type.Name.Contains("交换机", StringComparison.Ordinal) && request.ElectricalPorts <= 0)
        {
            return (null, BadRequest(new ApiError("交换机类型的端口数量必须大于 0")));
        }
        if (request.UplinkPorts is < 0 or > 1000)
        {
            return (null, BadRequest(new ApiError("上联口数量无效")));
        }

        var code = request.Code.Trim();
        var exists = await _db.SwitchModels.AnyAsync(
            x => x.Code == code && (excludeId == null || x.Id != excludeId), ct);
        if (exists)
        {
            return (null, Conflict(new ApiError("设备编码已存在")));
        }

        return (type, null);
    }

    /// <summary>设备类型编码 → 名称；样本只存编码，名称在读取时实时解析。</summary>
    private async Task<Dictionary<string, string>> GetEquipmentTypeNamesAsync(CancellationToken ct)
    {
        var items = await _db.DictionaryItems.AsNoTracking()
            .Where(x => x.Type.Code == "EQUIPMENT_TYPE")
            .Select(x => new { x.Code, x.Name })
            .ToListAsync(ct);

        return items
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<string?> GetEquipmentTypeNameAsync(string? typeCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(typeCode))
        {
            return null;
        }

        return await _db.DictionaryItems.AsNoTracking()
            .Where(x => x.Type.Code == "EQUIPMENT_TYPE" && x.Code == typeCode)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(ct);
    }

    private static SwitchModelDto ToDto(SwitchModel model, string? typeName) => new(
        model.Id,
        model.Code,
        model.Name,
        model.Discipline,
        model.TypeCode,
        typeName,
        model.ElectricalPorts,
        model.UplinkPorts,
        model.IsActive,
        model.SortOrder,
        model.Remark);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
