using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WinDesign.Api.Common;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Permissions;
using WinDesign.Domain.Entities;
using WinDesign.Infrastructure;

namespace WinDesign.Api.Controllers;

[Route("api/dictionaries")]
public sealed class DictionariesController : ApiControllerBase
{
    /// <summary>设备类型使用的编码类型；设备实例以类型编码字符串引用其中的编码项。</summary>
    private const string EquipmentTypeDictionaryCode = "EQUIPMENT_TYPE";

    private readonly WinDesignDbContext _db;

    public DictionariesController(WinDesignDbContext db)
    {
        _db = db;
    }

    [HttpGet("types")]
    [Authorize(Policy = AuthorizationPolicies.ClientAccess)]
    [RequirePermission("dictionaries.read")]
    public async Task<ActionResult<IReadOnlyList<DictionaryTypeDto>>> GetTypes(CancellationToken ct)
    {
        var types = await _db.DictionaryTypes
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .Select(x => new DictionaryTypeDto(
                x.Id, x.Code, x.Name, x.Description, x.IsSystem, x.SortOrder, x.Items.Count))
            .ToListAsync(ct);
        return Ok(types);
    }

    [HttpPost("types")]
    [Authorize(Policy = AuthorizationPolicies.AdminClient)]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("dictionaries.create")]
    public async Task<ActionResult<DictionaryTypeDto>> CreateType(
        [FromBody] CreateDictionaryTypeRequest request,
        CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiError("编码和名称不能为空"));
        }
        if (await _db.DictionaryTypes.AnyAsync(x => x.Code == code, ct))
        {
            return Conflict(new ApiError("编码类型已存在"));
        }

        var type = new DictionaryType
        {
            Code = code,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            SortOrder = request.SortOrder
        };
        _db.DictionaryTypes.Add(type);
        _db.AddAudit(CurrentUserId, CurrentUserName, "Create", "DictionaryType", type.Id.ToString(), code);
        await _db.SaveChangesAsync(ct);
        return Ok(new DictionaryTypeDto(type.Id, type.Code, type.Name, type.Description, type.IsSystem, type.SortOrder, 0));
    }

    [HttpPut("types/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminClient)]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("dictionaries.update")]
    public async Task<ActionResult<DictionaryTypeDto>> UpdateType(
        Guid id,
        [FromBody] UpdateDictionaryTypeRequest request,
        CancellationToken ct)
    {
        var type = await _db.DictionaryTypes.Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (type is null)
        {
            return NotFound(new ApiError("编码类型不存在"));
        }

        type.Name = request.Name.Trim();
        type.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        type.SortOrder = request.SortOrder;
        type.UpdatedAt = DateTime.UtcNow;
        _db.AddAudit(CurrentUserId, CurrentUserName, "Update", "DictionaryType", type.Id.ToString(), type.Code);
        await _db.SaveChangesAsync(ct);
        return Ok(new DictionaryTypeDto(type.Id, type.Code, type.Name, type.Description, type.IsSystem, type.SortOrder, type.Items.Count));
    }

    [HttpDelete("types/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminClient)]
    [Authorize(Roles = "Admin")]
    [RequirePermission("dictionaries.delete")]
    public async Task<IActionResult> DeleteType(Guid id, CancellationToken ct)
    {
        var type = await _db.DictionaryTypes.Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (type is null)
        {
            return NotFound(new ApiError("编码类型不存在"));
        }
        if (type.IsSystem)
        {
            return BadRequest(new ApiError("系统内置编码类型不能删除"));
        }

        _db.DictionaryItems.RemoveRange(type.Items);
        _db.DictionaryTypes.Remove(type);
        _db.AddAudit(CurrentUserId, CurrentUserName, "Delete", "DictionaryType", type.Id.ToString(), type.Code);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("types/{typeCode}/items")]
    [Authorize(Policy = AuthorizationPolicies.ClientAccess)]
    [RequirePermission("dictionaries.read")]
    public async Task<ActionResult<IReadOnlyList<DictionaryItemDto>>> GetItems(
        string typeCode,
        [FromQuery] bool includeInactive = true,
        CancellationToken ct = default)
    {
        var type = await _db.DictionaryTypes.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Code == typeCode.ToUpperInvariant(), ct);
        if (type is null)
        {
            return NotFound(new ApiError("编码类型不存在"));
        }

        var query = _db.DictionaryItems.AsNoTracking().Where(x => x.TypeId == type.Id);
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        var entities = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .ToListAsync(ct);
        return Ok(entities.Select(x => ToDto(x, type.Code)).ToList());
    }

    [HttpPost("types/{typeCode}/items")]
    [Authorize(Policy = AuthorizationPolicies.AdminClient)]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("dictionaries.create")]
    public async Task<ActionResult<DictionaryItemDto>> CreateItem(
        string typeCode,
        [FromBody] CreateDictionaryItemRequest request,
        CancellationToken ct)
    {
        var type = await _db.DictionaryTypes
            .SingleOrDefaultAsync(x => x.Code == typeCode.ToUpperInvariant(), ct);
        if (type is null)
        {
            return NotFound(new ApiError("编码类型不存在"));
        }

        var code = request.Code.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiError("编码和名称不能为空"));
        }
        if (await _db.DictionaryItems.AnyAsync(x => x.TypeId == type.Id && x.Code == code, ct))
        {
            return Conflict(new ApiError("该类型下编码已存在"));
        }

        var item = new DictionaryItem
        {
            TypeId = type.Id,
            Code = code,
            Name = request.Name.Trim(),
            SortOrder = request.SortOrder,
            ExtraJson = DictionaryItemExtras.SetDiscipline(EmptyToNull(request.ExtraJson), request.Discipline)
        };
        _db.DictionaryItems.Add(item);
        _db.AddAudit(CurrentUserId, CurrentUserName, "Create", "DictionaryItem", item.Id.ToString(), $"{type.Code}/{code}");
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(item, type.Code));
    }

    [HttpPut("items/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminClient)]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("dictionaries.update")]
    public async Task<ActionResult<DictionaryItemDto>> UpdateItem(
        Guid id,
        [FromBody] UpdateDictionaryItemRequest request,
        CancellationToken ct)
    {
        var item = await _db.DictionaryItems.Include(x => x.Type)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null)
        {
            return NotFound(new ApiError("编码项不存在"));
        }

        var oldCode = item.Code;
        var newCode = string.IsNullOrWhiteSpace(request.Code) ? oldCode : request.Code.Trim();
        if (!string.Equals(newCode, oldCode, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(newCode))
            {
                return BadRequest(new ApiError("编码不能为空"));
            }
            if (await _db.DictionaryItems.AnyAsync(
                    x => x.TypeId == item.TypeId && x.Id != id && x.Code == newCode, ct))
            {
                return Conflict(new ApiError("该类型下编码已存在"));
            }

            // 设备类型改编码时同步设备的类型引用，但位号是实例化时的快照，不能改。
            if (string.Equals(item.Type.Code, EquipmentTypeDictionaryCode, StringComparison.OrdinalIgnoreCase))
            {
                await SyncEquipmentTypeCodeAsync(oldCode, newCode, ct);
            }

            item.Code = newCode;
        }

        item.Name = request.Name.Trim();
        item.SortOrder = request.SortOrder;
        item.IsActive = request.IsActive;
        item.ExtraJson = DictionaryItemExtras.SetDiscipline(EmptyToNull(request.ExtraJson), request.Discipline);
        item.UpdatedAt = DateTime.UtcNow;
        _db.AddAudit(CurrentUserId, CurrentUserName, "Update", "DictionaryItem", item.Id.ToString(),
            $"{item.Type.Code}/{oldCode} -> {newCode}");
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(item, item.Type.Code));
    }

    /// <summary>统计引用了指定设备类型编码的设备数量（编码区分大小写存储，比较时忽略大小写）。</summary>
    private Task<int> CountEquipmentsUsingTypeAsync(string typeCode, CancellationToken ct)
    {
        var normalized = typeCode.ToUpperInvariant();
        return _db.Equipments.CountAsync(
            x => x.TypeCode != null && x.TypeCode.ToUpper() == normalized, ct);
    }

    /// <summary>
    /// 设备类型改编码后，把已实例化设备的类型引用同步到新编码。
    /// 只更新 TypeCode，不动 TagNo：位号包含旧编码，但它是实例化时的快照，必须保持不变。
    /// </summary>
    private async Task SyncEquipmentTypeCodeAsync(string oldCode, string newCode, CancellationToken ct)
    {
        var normalized = oldCode.ToUpperInvariant();
        var equipments = await _db.Equipments
            .Where(x => x.TypeCode != null && x.TypeCode.ToUpper() == normalized)
            .ToListAsync(ct);
        foreach (var equipment in equipments)
        {
            equipment.TypeCode = newCode;
        }
    }

    private static DictionaryItemDto ToDto(DictionaryItem item, string typeCode) => new(
        item.Id, item.TypeId, typeCode, item.Code, item.Name, item.SortOrder, item.IsActive, item.ExtraJson)
    {
        Discipline = DictionaryItemExtras.GetDiscipline(item.ExtraJson)
    };

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    [HttpDelete("items/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminClient)]
    [Authorize(Roles = "Admin,Designer")]
    [RequirePermission("dictionaries.delete")]
    public async Task<IActionResult> DeleteItem(Guid id, CancellationToken ct)
    {
        var item = await _db.DictionaryItems.Include(x => x.Type)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null)
        {
            return NotFound(new ApiError("编码项不存在"));
        }

        // 设备实例化后按编码字符串引用设备类型，删除会留下悬空类型，必须阻止。
        if (string.Equals(item.Type.Code, EquipmentTypeDictionaryCode, StringComparison.OrdinalIgnoreCase))
        {
            var equipmentCount = await CountEquipmentsUsingTypeAsync(item.Code, ct);
            if (equipmentCount > 0)
            {
                return BadRequest(new ApiError(
                    $"设备类型「{item.Code}」已被 {equipmentCount} 台设备使用，不能删除。"));
            }
        }

        _db.DictionaryItems.Remove(item);
        _db.AddAudit(CurrentUserId, CurrentUserName, "Delete", "DictionaryItem", item.Id.ToString(), $"{item.Type.Code}/{item.Code}");
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
