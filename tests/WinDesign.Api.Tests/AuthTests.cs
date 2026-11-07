using System.Net;
using System.Net.Http.Json;
using WinDesign.Contracts.Auth;

namespace WinDesign.Api.Tests;

public class AuthTests : ApiTestBase
{
    public AuthTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Login_WithAdmin_ReturnsTokenAndRoles()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = "admin",
            password = "admin123"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(login);
        Assert.False(string.IsNullOrWhiteSpace(login!.AccessToken));
        Assert.Contains("Admin", login.User.Roles);
        Assert.NotEmpty(login.User.Permissions);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = "admin",
            password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Users_WithoutToken_ReturnsUnauthorized()
    {
        using var anonymous = Factory.CreateClient();
        var response = await anonymous.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Designer_CannotAccessUserManagement()
    {
        await LoginAsync("designer", "designer123");

        var response = await Client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
