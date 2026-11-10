using System.Net.Http.Headers;
using System.Net.Http.Json;
using WinDesign.Contracts.Auth;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Plants;
using WinDesign.Contracts.Projects;
using WinDesign.Contracts.Workspaces;

namespace WinDesign.Api.Tests;

public abstract class ApiTestBase : IClassFixture<ApiFactory>
{
    protected ApiFactory Factory { get; }
    protected HttpClient Client { get; }
    protected WorkspaceResponse Workspace { get; private set; } = null!;
    protected Guid PlantId { get; private set; }

    protected ApiTestBase(ApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    /// <summary>只登录，不选择工作区，返回身份令牌（client = auth）。</summary>
    protected async Task<LoginResponse> LoginIdentityAsync(
        string userName = "admin",
        string password = "admin123")
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new { userName, password });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return login;
    }

    /// <summary>
    /// 登录并选择工作区，换取设计客户端令牌（client = design，带 plantId / projectId）。
    /// </summary>
    protected async Task LoginAsync(string userName = "admin", string password = "admin123")
    {
        await LoginIdentityAsync(userName, password);

        var workspaces = await Client.GetFromJsonAsync<List<WorkspacePlantDto>>("/api/auth/workspaces")
                         ?? new List<WorkspacePlantDto>();
        var plant = workspaces
            .FirstOrDefault(x => x.Projects.Any(p => p.Code == "DEMO-001"))
            ?? workspaces.First(x => x.Projects.Count > 0);
        var project = plant.Projects.FirstOrDefault(x => x.Code == "DEMO-001")
                      ?? plant.Projects[0];

        var select = await Client.PostAsJsonAsync("/api/auth/workspace", new
        {
            plantId = plant.Id,
            projectId = project.Id
        });
        select.EnsureSuccessStatusCode();
        Workspace = (await select.Content.ReadFromJsonAsync<WorkspaceResponse>())!;
        PlantId = Workspace.Plant.Id;
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Workspace.AccessToken);
    }

    /// <summary>
    /// 登录并进入管理模块，换取管理客户端令牌（client = admin，无 plantId / projectId）。
    /// 同时把 PlantId 设为 PLANT-A，供跨工厂配置类测试使用。
    /// </summary>
    protected async Task LoginManagementAsync(string userName = "admin", string password = "admin123")
    {
        await LoginIdentityAsync(userName, password);

        var response = await Client.PostAsync("/api/auth/management", null);
        response.EnsureSuccessStatusCode();
        var management = await response.Content.ReadFromJsonAsync<ManagementSessionResponse>();
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", management!.AccessToken);

        var plants = await Client.GetFromJsonAsync<List<PlantDto>>("/api/plants");
        PlantId = plants!.Single(x => x.Code == "PLANT-A").Id;
    }

    protected Task<ProjectDto> GetDemoProjectAsync()
    {
        // 设计客户端令牌不能再调用管理端的 /api/projects，直接从已选工作区构造测试数据。
        var project = Workspace.Project;
        return Task.FromResult(new ProjectDto
        {
            Id = project.Id,
            PlantId = PlantId,
            Code = project.Code,
            Name = project.Name,
            Phase = project.Phase,
            Status = project.Status,
            CreatedAt = DateTime.UtcNow
        });
    }
}
