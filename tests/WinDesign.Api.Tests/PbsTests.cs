using System.Net;
using System.Net.Http.Json;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Plants;

namespace WinDesign.Api.Tests;

public class PbsTests : ApiTestBase
{
    public PbsTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Pbs_InitialTree_ContainsTopLevelAndSeedNodes()
    {
        await LoginManagementAsync();

        var nodes = await GetNodesAsync();

        Assert.Contains(nodes, x => x.ParentId is null);
        Assert.Contains(nodes, x => x.NodeType == "Loop");
        Assert.DoesNotContain(nodes, x => x.NodeType == "Root");
        Assert.True(nodes.Count >= 3);
    }

    [Fact]
    public async Task Pbs_Crud_RoundTrip()
    {
        await LoginManagementAsync();
        var topLevel = await GetTopLevelAsync();
        var code = UniqueCode("T");

        var created = await CreateNodeAsync(topLevel.Id, code);
        try
        {
            Assert.Equal(code, created.Code);

            var update = await Client.PutAsJsonAsync($"/api/pbs/{created.Id}", new
            {
                parentId = topLevel.Id,
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
        await LoginManagementAsync();
        var topLevel = await GetTopLevelAsync();
        var code = UniqueCode("D");

        var created = await CreateNodeAsync(topLevel.Id, code);
        try
        {
            var duplicate = await Client.PostAsJsonAsync($"/api/plants/{PlantId}/pbs", new
            {
                parentId = topLevel.Id,
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
        await LoginManagementAsync();
        var topLevel = await GetTopLevelAsync();

        var parent = await CreateNodeAsync(topLevel.Id, UniqueCode("P"));
        var child = await CreateNodeAsync(parent.Id, UniqueCode("C"));
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
    public async Task Pbs_DeleteProjectRoot_ReturnsBadRequest()
    {
        await LoginManagementAsync();
        var plants = await Client.GetFromJsonAsync<List<PlantDto>>("/api/plants");
        var plantB = plants!.Single(x => x.Code == "PLANT-B");
        var nodes = await Client.GetFromJsonAsync<List<PbsNodeDto>>($"/api/plants/{plantB.Id}/pbs");
        // AREA-LNG 是 DEMO-003 的 PBS 根节点，且没有子节点，删除仍应被拒绝。
        var root = nodes!.Single(x => x.Code == "AREA-LNG");

        var response = await Client.DeleteAsync($"/api/pbs/{root.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DesignClient_CreatesLoopOrBuildingUnderUnit_Only()
    {
        // 设计端令牌：在装置下新增回路 / 建筑物。
        await LoginAsync();
        var unit = (await GetNodesAsync()).First(x => x.NodeType == "Unit" && x.Code == "CDU");
        var code = UniqueCode("L");

        var created = await Client.PostAsJsonAsync($"/api/units/{unit.Id}/pbs-children", new
        {
            code,
            name = "测试回路",
            nodeType = "Loop",
            description = ""
        });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var node = (await created.Content.ReadFromJsonAsync<PbsNodeDto>())!;
        Assert.Equal("Loop", node.NodeType);
        Assert.Equal(unit.Id, node.ParentId);

        // 该接口只接受 Loop / Building，区域等其他类型必须拒绝。
        var invalid = await Client.PostAsJsonAsync($"/api/units/{unit.Id}/pbs-children", new
        {
            code = UniqueCode("A"),
            name = "非法节点",
            nodeType = "Area",
            description = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        // 设计端没有 PBS 删除权限，切回管理令牌清理测试数据。
        await LoginManagementAsync();
        Assert.Equal(HttpStatusCode.NoContent,
            (await Client.DeleteAsync($"/api/pbs/{node.Id}")).StatusCode);
    }

    [Fact]
    public async Task Pbs_DeleteWithChildren_ReturnsBadRequest()
    {
        await LoginManagementAsync();
        var topLevel = await GetTopLevelAsync();

        var parent = await CreateNodeAsync(topLevel.Id, UniqueCode("P"));
        var child = await CreateNodeAsync(parent.Id, UniqueCode("C"));
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

    private async Task<List<PbsNodeDto>> GetNodesAsync()
    {
        var nodes = await Client.GetFromJsonAsync<List<PbsNodeDto>>($"/api/plants/{PlantId}/pbs");
        return nodes ?? new List<PbsNodeDto>();
    }

    private async Task<PbsNodeDto> GetTopLevelAsync() =>
        (await GetNodesAsync()).First(x => x.ParentId is null);

    private async Task<PbsNodeDto> CreateNodeAsync(Guid? parentId, string code)
    {
        var response = await Client.PostAsJsonAsync($"/api/plants/{PlantId}/pbs", new
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
