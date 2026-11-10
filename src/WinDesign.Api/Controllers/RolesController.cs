using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WinDesign.Api.Common;
using WinDesign.Contracts.Users;
using WinDesign.Infrastructure;

namespace WinDesign.Api.Controllers;

[Route("api/roles")]
[Authorize(Policy = AuthorizationPolicies.AdminClient)]
public sealed class RolesController : ApiControllerBase
{
    private readonly WinDesignDbContext _db;

    public RolesController(WinDesignDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> Get(CancellationToken ct)
    {
        var roles = await _db.Roles
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .Select(x => new RoleDto(x.Id, x.Code, x.Name, x.Description))
            .ToListAsync(ct);
        return Ok(roles);
    }
}
