using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WinDesign.Api.Services;
using WinDesign.Domain.Entities;

namespace WinDesign.UnitTests;

public class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "WinDesign.Tests",
        Audience = "WinDesign.Tests.Client",
        Key = "Test_Jwt_Key_For_WinDesign_At_Least_32_Chars",
        ExpiresHours = 2
    };

    private static readonly JwtTokenService Service = new(Microsoft.Extensions.Options.Options.Create(Options));

    [Fact]
    public void Create_ProducesValidTokenWithClaims()
    {
        var user = new User { UserName = "alice", DisplayName = "Alice" };

        var (token, expiresAt) = Service.Create(
            user,
            new[] { "Admin", "Designer" },
            new[] { "users.read", "projects.write" });

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(expiresAt > DateTime.UtcNow);

        var principal = Validate(token);
        Assert.Equal(user.Id.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("alice", principal.FindFirst(ClaimTypes.Name)?.Value);
        Assert.Equal("Alice", principal.FindFirst("displayName")?.Value);
        Assert.Contains(principal.FindAll(ClaimTypes.Role), c => c.Value == "Admin");
        Assert.Contains(principal.FindAll(ClaimTypes.Role), c => c.Value == "Designer");
        Assert.Contains(principal.FindAll("permission"), c => c.Value == "projects.write");
    }

    [Fact]
    public void Create_DeduplicatesRolesAndPermissions()
    {
        var user = new User { UserName = "bob", DisplayName = "Bob" };

        var (token, _) = Service.Create(
            user,
            new[] { "Viewer", "Viewer" },
            new[] { "reports.read", "reports.read" });

        var principal = Validate(token);
        Assert.Equal(1, principal.FindAll(ClaimTypes.Role).Count(c => c.Value == "Viewer"));
        Assert.Equal(1, principal.FindAll("permission").Count(c => c.Value == "reports.read"));
    }

    private static ClaimsPrincipal Validate(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Options.Issuer,
            ValidateAudience = true,
            ValidAudience = Options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Options.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        }, out _);
    }
}
