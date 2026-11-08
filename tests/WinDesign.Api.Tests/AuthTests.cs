using System.Net;
using System.Net.Http.Json;
using WinDesign.Contracts.Auth;
using WinDesign.Contracts.Workspaces;

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

    [Fact]
    public async Task Projects_WithoutWorkspace_ReturnsBadRequest()
    {
        var login = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = "admin",
            password = "admin123"
        });
        var loginResult = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);

        var response = await Client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Workspace_SelectingProjectFromAnotherPlant_ReturnsNotFound()
    {
        var login = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = "admin",
            password = "admin123"
        });
        var loginResult = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);

        var workspaces = await Client.GetFromJsonAsync<List<WorkspacePlantDto>>("/api/auth/workspaces")
                         ?? new List<WorkspacePlantDto>();
        var plantA = workspaces.First(x => x.Projects.Any(p => p.Code == "DEMO-001"));
        var plantB = workspaces.First(x => x.Projects.Any(p => p.Code == "DEMO-003"));
        var projectOfB = plantB.Projects.First(p => p.Code == "DEMO-003");

        var response = await Client.PostAsJsonAsync("/api/auth/workspace", new
        {
            plantId = plantA.Id,
            projectId = projectOfB.Id
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Workspaces_ForViewer_OnlyContainsJoinedProjects()
    {
        var login = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            userName = "viewer",
            password = "viewer123"
        });
        var loginResult = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);

        var workspaces = await Client.GetFromJsonAsync<List<WorkspacePlantDto>>("/api/auth/workspaces");

        var projects = workspaces!.SelectMany(x => x.Projects).Select(x => x.Code).ToList();
        Assert.Contains("DEMO-001", projects);
        Assert.DoesNotContain("DEMO-002", projects);
        Assert.DoesNotContain("DEMO-003", projects);
    }
}
