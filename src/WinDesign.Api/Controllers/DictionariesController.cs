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

        item.Name = request.Name.Trim();
        item.SortOrder = request.SortOrder;
        item.IsActive = request.IsActive;
        item.ExtraJson = DictionaryItemExtras.SetDiscipline(EmptyToNull(request.ExtraJson), request.Discipline);
        item.UpdatedAt = DateTime.UtcNow;
        _db.AddAudit(CurrentUserId, CurrentUserName, "Update", "DictionaryItem", item.Id.ToString(), $"{item.Type.Code}/{item.Code}");
        await _db.SaveChangesAsync(ct);
        return Ok(ToDto(item, item.Type.Code));
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

        _db.DictionaryItems.Remove(item);
        _db.AddAudit(CurrentUserId, CurrentUserName, "Delete", "DictionaryItem", item.Id.ToString(), $"{item.Type.Code}/{item.Code}");
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
