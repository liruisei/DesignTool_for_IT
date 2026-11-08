using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WinDesign.Domain.Entities;

namespace WinDesign.Api.Services;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) Create(
        User user,
        IEnumerable<string> roles,
        IEnumerable<string> permissions,
        Guid? plantId = null,
        Guid? projectId = null);
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public (string Token, DateTime ExpiresAt) Create(
        User user,
        IEnumerable<string> roles,
        IEnumerable<string> permissions,
        Guid? plantId = null,
        Guid? projectId = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new("displayName", user.DisplayName)
        };

        if (plantId is Guid plant)
        {
            claims.Add(new Claim("plantId", plant.ToString()));
        }

        if (projectId is Guid project)
        {
            claims.Add(new Claim("projectId", project.ToString()));
        }

        foreach (var role in roles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in permissions.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            claims.Add(new Claim("permission", permission));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddHours(_options.ExpiresHours);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
