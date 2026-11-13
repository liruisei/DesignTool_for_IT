using System.Net;
using System.Net.Http.Json;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Permissions;
using WinDesign.Contracts.Plants;
using WinDesign.Contracts.Projects;
using WinDesign.Contracts.Users;
using WinDesign.Contracts.Workspaces;

namespace WinDesign.Api.Tests;

public class ProjectMemberTests : ApiTestBase
{
    public ProjectMemberTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task AddMember_MakesProjectVisibleToUserInDesignClient()
    {
        var project = await CreateProjectAsync();
        var viewer = await GetUserAsync("viewer");

        var add = await Client.PostAsJsonAsync($"/api/projects/{project.Id}/members", new
        {
            userId = viewer.Id,
            roleInProject = "Viewer"
        });
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);

        // 换成 viewer 的身份令牌：工作区列表应出现刚分配的项目。
        await LoginIdentityAsync("viewer", "viewer123");
        var workspaces = await Client.GetFromJsonAsync<List<WorkspacePlantDto>>("/api/auth/workspaces");

        Assert.Contains(workspaces!, plant => plant.Projects.Any(x => x.Id == project.Id));
    }

    [Fact]
    public async Task AddMember_WithManagerRole_IsAllowed()
    {
        var project = await CreateProjectAsync();
        var viewer = await GetUserAsync("viewer");

        var response = await Client.PostAsJsonAsync($"/api/projects/{project.Id}/members", new
        {
            userId = viewer.Id,
            roleInProject = "Manager"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var member = await response.Content.ReadFromJsonAsync<ProjectMemberDto>();
        Assert.Equal("Manager", member!.RoleInProject);
    }

    [Fact]
    public async Task BulkGrant_AssignsRoleToMultipleUsers()
    {
        var project = await CreateProjectAsync();
        var viewer = await GetUserAsync("viewer");
        var designer = await GetUserAsync("designer");

        var response = await Client.PostAsJsonAsync($"/api/projects/{project.Id}/members/bulk", new
        {
            userIds = new[] { viewer.Id, designer.Id },
            roleInProject = "Designer"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var members = await response.Content.ReadFromJsonAsync<List<ProjectMemberDto>>();
        Assert.Equal(3, members!.Count); // 创建者（管理）+ 两个授权用户
        Assert.All(
            members.Where(x => x.UserId == viewer.Id || x.UserId == designer.Id),
            x => Assert.Equal("Designer", x.RoleInProject));
    }

    [Fact]
    public async Task RemoveOwner_ReturnsBadRequest()
    {
        var project = await CreateProjectAsync();
        var admin = await GetUserAsync("admin");

        var response = await Client.DeleteAsync($"/api/projects/{project.Id}/members/{admin.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DesignToken_CannotManageMembers()
    {
        await LoginAsync();

        var response = await Client.GetAsync($"/api/projects/{Workspace.Project.Id}/members");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RolePermissions_ControlGlobalCreatePermission()
    {
        await LoginManagementAsync();
        var roles = await Client.GetFromJsonAsync<List<RoleDto>>("/api/roles");
        var designerRole = roles!.Single(x => x.Code == "Designer");
        var current = await Client.GetFromJsonAsync<RolePermissionDto>(
            $"/api/roles/{designerRole.Id}/permissions");

        var withoutCreate = current!.PermissionCodes
            .Where(x => !string.Equals(x, "equipment.create", StringComparison.OrdinalIgnoreCase))
            .ToList();

        try
        {
            var update = await Client.PutAsJsonAsync(
                $"/api/roles/{designerRole.Id}/permissions",
                new { permissionCodes = withoutCreate });
            update.EnsureSuccessStatusCode();

            await LoginAsync("designer", "designer123");
            var create = await Client.PostAsJsonAsync(
                $"/api/projects/{Workspace.Project.Id}/equipments",
                new { tagNo = "PERM-001", name = "权限测试设备" });

            Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        }
        finally
        {
            await LoginManagementAsync();
            await Client.PutAsJsonAsync(
                $"/api/roles/{designerRole.Id}/permissions",
                new { permissionCodes = current.PermissionCodes });
        }
    }

    [Fact]
    public async Task AdminRole_CannotDropUserManagementPermissions()
    {
        await LoginManagementAsync();
        var roles = await Client.GetFromJsonAsync<List<RoleDto>>("/api/roles");
        var adminRole = roles!.Single(x => x.Code == "Admin");

        var response = await Client.PutAsJsonAsync(
            $"/api/roles/{adminRole.Id}/permissions",
            new
            {
                permissionCodes = new[]
                {
                    PermissionCatalog.Code("projects", "read"),
                    PermissionCatalog.Code("projects", "update")
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>以管理模块令牌新建一个唯一项目，创建者（admin）自动成为项目负责人并拥有管理角色。</summary>
    private async Task<ProjectDto> CreateProjectAsync()
    {
        await LoginManagementAsync();

        var plants = await Client.GetFromJsonAsync<List<PlantDto>>("/api/plants");
        var plantA = plants!.Single(x => x.Code == "PLANT-A");
        var code = "MEM-" + Guid.NewGuid().ToString("N")[..8];

        var response = await Client.PostAsJsonAsync("/api/projects", new
        {
            plantId = plantA.Id,
            code,
            name = "项目成员测试项目"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectDto>())!;
    }

    private async Task<UserDto> GetUserAsync(string userName)
    {
        var users = await Client.GetFromJsonAsync<PagedResult<UserDto>>("/api/users?pageSize=200");
        return users!.Items.Single(x => x.UserName == userName);
    }
}
