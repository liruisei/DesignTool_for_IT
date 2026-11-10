using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WinDesign.Contracts.Auth;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Plants;
using WinDesign.Contracts.Projects;
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
    public async Task Projects_WithoutManagementClient_ReturnsForbidden_ForIdentityToken()
    {
        // 身份令牌（client = auth）尚未选择任何客户端，不能直接访问管理端接口。
        await LoginIdentityAsync("designer", "designer123");

        var response = await Client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DesignToken_CannotCallAdminClientEndpoints()
    {
        // 即使是管理员，只要拿到的是设计客户端令牌，也不能调用管理端写接口。
        await LoginAsync();

        var project = await Client.PostAsJsonAsync("/api/projects", new
        {
            code = "DENY-" + Guid.NewGuid().ToString("N")[..8],
            name = "设计令牌不应创建项目"
        });
        Assert.Equal(HttpStatusCode.Forbidden, project.StatusCode);

        var pbs = await Client.PostAsJsonAsync($"/api/plants/{PlantId}/pbs", new
        {
            parentId = (Guid?)null,
            code = "DENY-" + Guid.NewGuid().ToString("N")[..8],
            name = "设计令牌不应创建 PBS",
            nodeType = "Area",
            sortOrder = 999,
            description = ""
        });
        Assert.Equal(HttpStatusCode.Forbidden, pbs.StatusCode);
    }

    [Fact]
    public async Task ManagementToken_CannotCallDesignEquipmentEndpoints()
    {
        // 管理客户端只做配置；索引/设备数据属于设计客户端。
        await LoginManagementAsync();

        var response = await Client.GetAsync($"/api/projects/{Guid.NewGuid()}/equipments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ManagementToken_CanReadDesignEndpoints()
    {
        // 管理端的 PBS 管理页需要读取 PBS 与设备数据，因此设计类读接口对管理令牌开放。
        await LoginManagementAsync();

        var pbs = await Client.GetAsync($"/api/plants/{PlantId}/pbs");
        Assert.Equal(HttpStatusCode.OK, pbs.StatusCode);
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

    [Fact]
    public async Task Management_ForAdmin_AllowsCrossPlantSystemAccess()
    {
        await LoginIdentityAsync();
        var response = await Client.PostAsync("/api/auth/management", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var management = await response.Content.ReadFromJsonAsync<ManagementSessionResponse>();
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", management!.AccessToken);

        // 管理模块可以看到所有工厂的项目（跨工厂）。
        var projects = await Client.GetFromJsonAsync<PagedResult<ProjectDto>>("/api/projects");
        Assert.Contains(projects!.Items, x => x.Code == "DEMO-001");
        Assert.Contains(projects.Items, x => x.Code == "DEMO-003");

        // 管理模块可以管理任意工厂的 PBS。
        var plants = await Client.GetFromJsonAsync<List<PlantDto>>("/api/plants");
        var plantB = plants!.Single(x => x.Code == "PLANT-B");
        var pbs = await Client.GetAsync($"/api/plants/{plantB.Id}/pbs");
        Assert.Equal(HttpStatusCode.OK, pbs.StatusCode);
    }

    [Fact]
    public async Task Management_ForDesigner_ReturnsForbidden()
    {
        await LoginIdentityAsync("designer", "designer123");

        var response = await Client.PostAsync("/api/auth/management", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Management_CanCreateProjectUnderSelectedPlant()
    {
        await LoginIdentityAsync();
        var managementResponse = await Client.PostAsync("/api/auth/management", null);
        var management = await managementResponse.Content.ReadFromJsonAsync<ManagementSessionResponse>();
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", management!.AccessToken);

        var plants = await Client.GetFromJsonAsync<List<PlantDto>>("/api/plants");
        var plant = plants!.Single(x => x.Code == "PLANT-B");
        var nodes = await Client.GetFromJsonAsync<List<PbsNodeDto>>($"/api/plants/{plant.Id}/pbs");
        var root = nodes!.Single(x => x.ParentId is null);
        var code = "MGMT-" + Guid.NewGuid().ToString("N")[..8];

        var response = await Client.PostAsJsonAsync("/api/projects", new
        {
            plantId = plant.Id,
            code,
            name = "管理模块新建项目",
            pbsRootNodeId = root.Id
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.Equal(plant.Id, created!.PlantId);
        Assert.Equal(root.Id, created.PbsRootNodeId);
    }

    [Fact]
    public async Task CreateProject_WithoutPbsRoot_ReturnsBadRequest()
    {
        await LoginIdentityAsync();
        var managementResponse = await Client.PostAsync("/api/auth/management", null);
        var management = await managementResponse.Content.ReadFromJsonAsync<ManagementSessionResponse>();
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", management!.AccessToken);

        var plants = await Client.GetFromJsonAsync<List<PlantDto>>("/api/plants");
        var plant = plants!.Single(x => x.Code == "PLANT-B");

        var response = await Client.PostAsJsonAsync("/api/projects", new
        {
            plantId = plant.Id,
            code = "NO-ROOT-" + Guid.NewGuid().ToString("N")[..8],
            name = "缺少 PBS 根节点"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProject_WithNonTopLevelPbsRoot_ReturnsBadRequest()
    {
        await LoginManagementAsync();

        var nodes = await Client.GetFromJsonAsync<List<PbsNodeDto>>($"/api/plants/{PlantId}/pbs");
        var child = nodes!.First(x => x.ParentId is not null);

        var response = await Client.PostAsJsonAsync("/api/projects", new
        {
            plantId = PlantId,
            code = "BAD-ROOT-" + Guid.NewGuid().ToString("N")[..8],
            name = "根节点不是顶层节点",
            pbsRootNodeId = child.Id
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
