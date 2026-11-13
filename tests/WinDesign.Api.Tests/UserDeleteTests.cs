using System.Net;
using System.Net.Http.Json;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Plants;
using WinDesign.Contracts.Projects;
using WinDesign.Contracts.Users;

namespace WinDesign.Api.Tests;

public class UserDeleteTests : ApiTestBase
{
    public UserDeleteTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task DeleteUser_RemovesUser()
    {
        await LoginManagementAsync();

        var userName = "del-" + Guid.NewGuid().ToString("N")[..8];
        var create = await Client.PostAsJsonAsync("/api/users", new
        {
            userName,
            displayName = "待删除用户",
            password = "delete123",
            roles = new[] { "Viewer" }
        });
        create.EnsureSuccessStatusCode();
        var user = await create.Content.ReadFromJsonAsync<UserDto>();

        var delete = await Client.DeleteAsync($"/api/users/{user!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var users = await Client.GetFromJsonAsync<PagedResult<UserDto>>($"/api/users?search={userName}");
        Assert.Empty(users!.Items);
    }

    [Fact]
    public async Task DeleteUser_TransfersOwnedProjectsToOperator()
    {
        await LoginManagementAsync();

        // 创建一个新管理员，并让它成为项目负责人。
        var userName = "own-" + Guid.NewGuid().ToString("N")[..8];
        var create = await Client.PostAsJsonAsync("/api/users", new
        {
            userName,
            displayName = "项目负责人",
            password = "owner12345",
            roles = new[] { "Admin" }
        });
        create.EnsureSuccessStatusCode();
        var owner = await create.Content.ReadFromJsonAsync<UserDto>();

        await LoginManagementAsync(userName, "owner12345");
        var plants = await Client.GetFromJsonAsync<List<PlantDto>>("/api/plants");
        var plantA = plants!.Single(x => x.Code == "PLANT-A");
        var projectCode = "OWN-" + Guid.NewGuid().ToString("N")[..8];
        var projectResponse = await Client.PostAsJsonAsync("/api/projects", new
        {
            plantId = plantA.Id,
            code = projectCode,
            name = "负责人转移测试项目"
        });
        projectResponse.EnsureSuccessStatusCode();

        // 换回系统管理员删除该用户：项目负责人应转给系统管理员。
        await LoginManagementAsync();
        var delete = await Client.DeleteAsync($"/api/users/{owner!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var admin = (await Client.GetFromJsonAsync<PagedResult<UserDto>>("/api/users?search=admin"))!
            .Items.Single(x => x.UserName == "admin");
        var projects = await Client.GetFromJsonAsync<PagedResult<ProjectDto>>(
            $"/api/projects?search={projectCode}");

        Assert.Equal(admin.Id, projects!.Items.Single().OwnerUserId);
    }

    [Fact]
    public async Task DeleteSelf_ReturnsBadRequest()
    {
        await LoginManagementAsync();

        var users = await Client.GetFromJsonAsync<PagedResult<UserDto>>("/api/users?search=admin");
        var admin = users!.Items.Single(x => x.UserName == "admin");

        var response = await Client.DeleteAsync($"/api/users/{admin.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
