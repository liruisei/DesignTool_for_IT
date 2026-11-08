using System.Net.Http.Headers;
using System.Net.Http.Json;
using WinDesign.Contracts.Auth;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Projects;
using WinDesign.Contracts.Workspaces;

namespace WinDesign.Api.Tests;

public abstract class ApiTestBase : IClassFixture<ApiFactory>
{
    protected ApiFactory Factory { get; }
    protected HttpClient Client { get; }
    protected WorkspaceResponse Workspace { get; private set; } = null!;
    protected Guid PlantId => Workspace.Plant.Id;

    protected ApiTestBase(ApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    /// <summary>
    /// 登录并选择工作区：登录只拿身份令牌，选择「工厂 + 项目」后再换取带范围的令牌。
    /// </summary>
    protected async Task LoginAsync(string userName = "admin", string password = "admin123")
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new { userName, password });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);

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
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Workspace.AccessToken);
    }

    protected async Task<ProjectDto> GetDemoProjectAsync()
    {
        var result = await Client.GetFromJsonAsync<PagedResult<ProjectDto>>("/api/projects");
        return result!.Items.Single(x => x.Code == "DEMO-001");
    }
}
