using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WinDesign.Api.Common;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Projects;
using WinDesign.Infrastructure;

namespace WinDesign.Api.Controllers;

[Route("api/dashboard")]
public sealed class DashboardController : ApiControllerBase
{
    private readonly WinDesignDbContext _db;

    public DashboardController(WinDesignDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken ct)
    {
        if (CurrentPlantId is not Guid plantId || CurrentProjectId is not Guid projectId)
        {
            return BadRequest(new ApiError("请先选择工作区（工厂与项目）"));
        }

        var userId = CurrentUserId;
        var isAdmin = User.IsInRole("Admin");

        var projectQuery = _db.Projects.AsNoTracking().Where(x => x.PlantId == plantId);
        if (!isAdmin)
        {
            projectQuery = projectQuery.Where(x => x.Members.Any(m => m.UserId == userId));
        }

        var userCount = await _db.Users.CountAsync(ct);
        var projectCount = await projectQuery.CountAsync(ct);
        var dictionaryTypeCount = await _db.DictionaryTypes.CountAsync(ct);
        var equipmentCount = await _db.Equipments
            .Where(x => x.PlantId == plantId &&
                        x.ProjectEquipments.Any(link => link.ProjectId == projectId))
            .CountAsync(ct);

        var recent = await projectQuery
            .Include(x => x.Owner)
            .Include(x => x.Plant)
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new ProjectDto
            {
                Id = x.Id,
                PlantId = x.PlantId,
                PlantName = x.Plant != null ? x.Plant.Name : null,
                Code = x.Code,
                Name = x.Name,
                ClientName = x.ClientName,
                Location = x.Location,
                Phase = x.Phase,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                Status = x.Status.ToString(),
                Description = x.Description,
                OwnerUserId = x.OwnerUserId,
                OwnerName = x.Owner != null ? x.Owner.DisplayName : null,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new DashboardDto(userCount, projectCount, dictionaryTypeCount, equipmentCount, recent));
    }
}
