using System.Net;
using System.Net.Http.Json;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Pbs;

namespace WinDesign.Api.Tests;

public class EquipmentTests : ApiTestBase
{
    public EquipmentTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Equipment_CrudAndFilters_RoundTrip()
    {
        await LoginAsync();
        var project = await GetDemoProjectAsync();
        var root = await GetRootAsync(project.Id);
        var tagNo = UniqueTag();

        var created = await CreateEquipmentAsync(project.Id, root.Id, tagNo);
        try
        {
            Assert.Equal(tagNo, created.TagNo);
            Assert.Equal(root.Name, created.PbsNodeName);

            var duplicate = await Client.PostAsJsonAsync($"/api/projects/{project.Id}/equipments", new
            {
                pbsNodeId = root.Id,
                tagNo,
                name = "重复设备",
                typeCode = "PT",
                discipline = "Instrument",
                status = "Planned",
                location = "",
                description = ""
            });
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

            var filtered = await Client.GetFromJsonAsync<PagedResult<EquipmentDto>>(
                $"/api/projects/{project.Id}/equipments?discipline=Instrument&status=Planned&pbsNodeId={root.Id}");
            Assert.Contains(filtered!.Items, x => x.TagNo == tagNo);

            var update = await Client.PutAsJsonAsync($"/api/equipments/{created.Id}", new
            {
                pbsNodeId = root.Id,
                tagNo,
                name = "测试设备-更新",
                typeCode = "PT",
                discipline = "Instrument",
                status = "Designed",
                location = "测试区",
                description = "updated"
            });
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            var updated = await update.Content.ReadFromJsonAsync<EquipmentDto>();
            Assert.Equal("Designed", updated!.Status);
        }
        finally
        {
            await Client.DeleteAsync($"/api/equipments/{created.Id}");
        }
    }

    [Fact]
    public async Task Equipment_WithUnknownPbs_ReturnsBadRequest()
    {
        await LoginAsync();
        var project = await GetDemoProjectAsync();

        var response = await Client.PostAsJsonAsync($"/api/projects/{project.Id}/equipments", new
        {
            pbsNodeId = Guid.NewGuid(),
            tagNo = UniqueTag(),
            name = "无效 PBS 设备",
            typeCode = "PT",
            discipline = "Instrument",
            status = "Planned",
            location = "",
            description = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<PbsNodeDto> GetRootAsync(Guid projectId)
    {
        var nodes = await Client.GetFromJsonAsync<List<PbsNodeDto>>($"/api/projects/{projectId}/pbs");
        return nodes!.Single(x => x.ParentId is null);
    }

    private async Task<EquipmentDto> CreateEquipmentAsync(Guid projectId, Guid pbsNodeId, string tagNo)
    {
        var response = await Client.PostAsJsonAsync($"/api/projects/{projectId}/equipments", new
        {
            pbsNodeId,
            tagNo,
            name = "测试压力变送器",
            typeCode = "PT",
            discipline = "Instrument",
            status = "Planned",
            location = "测试区",
            description = "integration test"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EquipmentDto>())!;
    }

    private static string UniqueTag() => "TEST-" + Guid.NewGuid().ToString("N")[..8];
}
