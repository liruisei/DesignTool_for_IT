using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WinDesign.Api.Common;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Plants;
using WinDesign.Domain.Entities;
using WinDesign.Infrastructure;

namespace WinDesign.Api.Controllers;

[Route("api/plants")]
[Authorize(Policy = AuthorizationPolicies.AdminClient)]
public sealed class PlantsController : ApiControllerBase
{
    private readonly WinDesignDbContext _db;

    public PlantsController(WinDesignDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlantDto>>> Get(CancellationToken ct)
    {
        var userId = CurrentUserId;
        var isAdmin = User.IsInRole("Admin");

        var plants = await _db.Plants
            .Include(x => x.Projects)
                .ThenInclude(x => x.Members)
            .Include(x => x.PbsNodes)
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .ToListAsync(ct);

        var result = plants
            .Where(plant => isAdmin || plant.Projects.Any(p => p.Members.Any(m => m.UserId == userId)))
            .Select(ToDto)
            .ToList();

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlantDto>> GetById(Guid id, CancellationToken ct)
    {
        var plant = await _db.Plants
            .Include(x => x.Projects)
                .ThenInclude(x => x.Members)
            .Include(x => x.PbsNodes)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct);

        if (plant is null)
        {
            return NotFound(new ApiError("工厂不存在"));
        }

        return Ok(ToDto(plant));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Designer")]
    public async Task<ActionResult<PlantDto>> Create(
        [FromBody] CreatePlantRequest request,
        CancellationToken ct)
    {
        var code = request.Code.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiError("工厂编码和名称不能为空"));
        }
        if (await _db.Plants.AnyAsync(x => x.Code == code, ct))
        {
            return Conflict(new ApiError("工厂编码已存在"));
        }

        var plant = new Plant
        {
            Code = code,
            Name = request.Name.Trim(),
            ClientName = Clean(request.ClientName),
            Location = Clean(request.Location),
            Description = Clean(request.Description)
        };
        _db.Plants.Add(plant);
        _db.AddAudit(CurrentUserId, CurrentUserName, "Create", "Plant", plant.Id.ToString(), code);
        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(plant));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Designer")]
    public async Task<ActionResult<PlantDto>> Update(
        Guid id,
        [FromBody] UpdatePlantRequest request,
        CancellationToken ct)
    {
        var plant = await _db.Plants
            .Include(x => x.Projects)
                .ThenInclude(x => x.Members)
            .Include(x => x.PbsNodes)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (plant is null)
        {
            return NotFound(new ApiError("工厂不存在"));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiError("工厂名称不能为空"));
        }

        plant.Name = request.Name.Trim();
        plant.ClientName = Clean(request.ClientName);
        plant.Location = Clean(request.Location);
        plant.Description = Clean(request.Description);
        plant.IsActive = request.IsActive;
        plant.UpdatedAt = DateTime.UtcNow;

        _db.AddAudit(CurrentUserId, CurrentUserName, "Update", "Plant", plant.Id.ToString(), plant.Code);
        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(plant));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var plant = await _db.Plants.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (plant is null)
        {
            return NotFound(new ApiError("工厂不存在"));
        }
        if (await _db.Projects.AnyAsync(x => x.PlantId == id, ct))
        {
            return BadRequest(new ApiError("工厂下还有项目，不能删除"));
        }
        if (await _db.PbsNodes.AnyAsync(x => x.PlantId == id, ct))
        {
            return BadRequest(new ApiError("工厂下还有 PBS 节点，不能删除"));
        }
        if (await _db.Equipments.AnyAsync(x => x.PlantId == id, ct))
        {
            return BadRequest(new ApiError("工厂下还有设备，不能删除"));
        }

        _db.Plants.Remove(plant);
        _db.AddAudit(CurrentUserId, CurrentUserName, "Delete", "Plant", plant.Id.ToString(), plant.Code);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static PlantDto ToDto(Plant plant) => new(
        plant.Id,
        plant.Code,
        plant.Name,
        plant.ClientName,
        plant.Location,
        plant.Description,
        plant.IsActive,
        plant.Projects.Count,
        plant.PbsNodes.Count,
        plant.CreatedAt);

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
