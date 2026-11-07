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
        var userCount = await _db.Users.CountAsync(ct);
        var projectCount = await _db.Projects.CountAsync(ct);
        var dictionaryTypeCount = await _db.DictionaryTypes.CountAsync(ct);
        var equipmentCount = await _db.Equipments.CountAsync(ct);

        var recent = await _db.Projects
            .Include(x => x.Owner)
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new ProjectDto
            {
                Id = x.Id,
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
