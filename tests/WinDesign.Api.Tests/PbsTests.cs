using System.Net;
using System.Net.Http.Json;
using WinDesign.Contracts.Pbs;

namespace WinDesign.Api.Tests;

public class PbsTests : ApiTestBase
{
    public PbsTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Pbs_InitialTree_ContainsRootAndSeedNodes()
    {
        await LoginAsync();
        var project = await GetDemoProjectAsync();

        var nodes = await GetNodesAsync(project.Id);

        Assert.Contains(nodes, x => x.ParentId is null);
        Assert.Contains(nodes, x => x.NodeType == "Loop");
        Assert.True(nodes.Count >= 4);
    }

    [Fact]
    public async Task Pbs_Crud_RoundTrip()
    {
        await LoginAsync();
        var project = await GetDemoProjectAsync();
        var root = await GetRootAsync(project.Id);
        var code = UniqueCode("T");

        var created = await CreateNodeAsync(project.Id, root.Id, code);
        try
        {
            Assert.Equal(code, created.Code);

            var update = await Client.PutAsJsonAsync($"/api/pbs/{created.Id}", new
            {
                parentId = root.Id,
                code,
                name = "测试节点-更新",
                nodeType = "SubSystem",
                sortOrder = 901,
                description = "updated"
            });
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            var updated = await update.Content.ReadFromJsonAsync<PbsNodeDto>();
            Assert.Equal("测试节点-更新", updated!.Name);
        }
        finally
        {
            await Client.DeleteAsync($"/api/pbs/{created.Id}");
        }
    }

    [Fact]
    public async Task Pbs_DuplicateCode_ReturnsConflict()
    {
        await LoginAsync();
        var project = await GetDemoProjectAsync();
        var root = await GetRootAsync(project.Id);
        var code = UniqueCode("D");

        var created = await CreateNodeAsync(project.Id, root.Id, code);
        try
        {
            var duplicate = await Client.PostAsJsonAsync($"/api/projects/{project.Id}/pbs", new
            {
                parentId = root.Id,
                code,
                name = "重复节点",
                nodeType = "SubSystem",
                sortOrder = 902,
                description = ""
            });

            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        }
        finally
        {
            await Client.DeleteAsync($"/api/pbs/{created.Id}");
        }
    }

    [Fact]
    public async Task Pbs_Cycle_ReturnsBadRequest()
    {
        await LoginAsync();
        var project = await GetDemoProjectAsync();
        var root = await GetRootAsync(project.Id);

        var parent = await CreateNodeAsync(project.Id, root.Id, UniqueCode("P"));
        var child = await CreateNodeAsync(project.Id, parent.Id, UniqueCode("C"));
        try
        {
            var response = await Client.PutAsJsonAsync($"/api/pbs/{parent.Id}", new
            {
                parentId = child.Id,
                code = parent.Code,
                name = parent.Name,
                nodeType = "SubSystem",
                sortOrder = parent.SortOrder,
                description = ""
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await Client.DeleteAsync($"/api/pbs/{child.Id}");
            await Client.DeleteAsync($"/api/pbs/{parent.Id}");
        }
    }

    [Fact]
    public async Task Pbs_DeleteWithChildren_ReturnsBadRequest()
    {
        await LoginAsync();
        var project = await GetDemoProjectAsync();
        var root = await GetRootAsync(project.Id);

        var parent = await CreateNodeAsync(project.Id, root.Id, UniqueCode("P"));
        var child = await CreateNodeAsync(project.Id, parent.Id, UniqueCode("C"));
        try
        {
            var response = await Client.DeleteAsync($"/api/pbs/{parent.Id}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await Client.DeleteAsync($"/api/pbs/{child.Id}");
            await Client.DeleteAsync($"/api/pbs/{parent.Id}");
        }
    }

    private async Task<List<PbsNodeDto>> GetNodesAsync(Guid projectId)
    {
        var nodes = await Client.GetFromJsonAsync<List<PbsNodeDto>>($"/api/projects/{projectId}/pbs");
        return nodes ?? new List<PbsNodeDto>();
    }

    private async Task<PbsNodeDto> GetRootAsync(Guid projectId) =>
        (await GetNodesAsync(projectId)).Single(x => x.ParentId is null);

    private async Task<PbsNodeDto> CreateNodeAsync(Guid projectId, Guid? parentId, string code)
    {
        var response = await Client.PostAsJsonAsync($"/api/projects/{projectId}/pbs", new
        {
            parentId,
            code,
            name = "测试节点",
            nodeType = "SubSystem",
            sortOrder = 900,
            description = "integration test"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PbsNodeDto>())!;
    }

    private static string UniqueCode(string prefix) =>
        prefix + Guid.NewGuid().ToString("N")[..8];
}
