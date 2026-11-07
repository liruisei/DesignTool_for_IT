using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WinDesign.Api.Common;
using WinDesign.Api.Services;
using WinDesign.Contracts.Auth;
using WinDesign.Contracts.Common;
using WinDesign.Infrastructure;

namespace WinDesign.Api.Controllers;

[Route("api/auth")]
public sealed class AuthController : ApiControllerBase
{
    private readonly WinDesignDbContext _db;
    private readonly IJwtTokenService _jwt;

    public AuthController(WinDesignDbContext db, IJwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ApiError("请输入用户名和密码"));
        }

        var user = await _db.Users
            .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                    .ThenInclude(x => x.RolePermissions)
                        .ThenInclude(x => x.Permission)
            .SingleOrDefaultAsync(x => x.UserName == request.UserName, ct);

        if (user is null || !user.IsActive || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new ApiError("用户名或密码错误"));
        }

        var roles = user.UserRoles
            .Select(x => x.Role.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var permissions = user.IsSystemAdmin
            ? await _db.Permissions.Select(x => x.Code).ToListAsync(ct)
            : user.UserRoles
                .SelectMany(x => x.Role.RolePermissions)
                .Select(x => x.Permission.Code)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        user.LastLoginAt = DateTime.UtcNow;
        _db.AddAudit(user.Id, user.UserName, "Login", "User", user.Id.ToString());
        await _db.SaveChangesAsync(ct);

        var (token, expiresAt) = _jwt.Create(user, roles, permissions);
        return Ok(new LoginResponse(
            token,
            expiresAt,
            new CurrentUserDto(user.Id, user.UserName, user.DisplayName, roles, permissions)));
    }

    [HttpGet("me")]
    public ActionResult<CurrentUserDto> Me()
    {
        var roles = User.FindAll(ClaimTypes.Role)
            .Select(x => x.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var displayName = User.FindFirstValue("displayName") ?? CurrentUserName;
        return Ok(new CurrentUserDto(CurrentUserId, CurrentUserName, displayName, roles, CurrentPermissions));
    }
}
