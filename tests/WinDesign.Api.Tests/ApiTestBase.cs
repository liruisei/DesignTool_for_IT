using System.Net.Http.Headers;
using System.Net.Http.Json;
using WinDesign.Contracts.Auth;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Projects;

namespace WinDesign.Api.Tests;

public abstract class ApiTestBase : IClassFixture<ApiFactory>
{
    protected ApiFactory Factory { get; }
    protected HttpClient Client { get; }

    protected ApiTestBase(ApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    protected async Task LoginAsync(string userName = "admin", string password = "admin123")
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new { userName, password });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);
    }

    protected async Task<ProjectDto> GetDemoProjectAsync()
    {
        var result = await Client.GetFromJsonAsync<PagedResult<ProjectDto>>("/api/projects");
        return result!.Items.Single(x => x.Code == "DEMO-001");
    }
}
