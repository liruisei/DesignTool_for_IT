using System.Net;
using System.Net.Http.Json;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Equipment;

namespace WinDesign.Api.Tests;

public class EquipmentSpecTests : ApiTestBase
{
    public EquipmentSpecTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task SeedSpec_ParsesParametersFromContentJson()
    {
        await LoginAsync();
        var project = await GetDemoProjectAsync();

        var list = await Client.GetFromJsonAsync<PagedResult<EquipmentSpecDto>>(
            $"/api/projects/{project.Id}/specs?search=SPEC-PT-1001");

        var spec = Assert.Single(list!.Items, x => x.SpecCode == "SPEC-PT-1001");
        Assert.Equal("PT-1001", spec.EquipmentTagNo);
        Assert.Equal(3, spec.Parameters!.Count);
        Assert.Contains(spec.Parameters, x => x.Key == "range" && x.Value == "0~1.6MPa");
        Assert.Null(spec.ContentWarning);
        Assert.Equal(1, spec.Version);
    }

    [Fact]
    public async Task Spec_Crud_And_UnblocksEquipmentDelete()
    {
        await LoginAsync();
        var project = await GetDemoProjectAsync();
        var equipment = await CreateEquipmentAsync(project.Id);
        var code = "SPEC-" + Guid.NewGuid().ToString("N")[..8];

        var create = await Client.PostAsJsonAsync($"/api/equipments/{equipment.Id}/specs", new
        {
            specCode = code,
            specName = "测试规格书",
            parameters = new[]
            {
                new { key = "range", value = "0~1.6MPa" },
                new { key = "output", value = "4~20mA" }
            }
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<EquipmentSpecDto>())!;
        Assert.Equal(2, created.Parameters!.Count);
        Assert.Equal(equipment.TagNo, created.EquipmentTagNo);

        var duplicate = await Client.PostAsJsonAsync($"/api/equipments/{equipment.Id}/specs", new
        {
            specCode = code,
            specName = "重复规格书",
            parameters = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var invalidParameters = await Client.PutAsJsonAsync($"/api/specs/{created.Id}", new
        {
            specCode = code,
            specName = "测试规格书",
            parameters = new[] { new { key = "", value = "值" } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidParameters.StatusCode);

        var update = await Client.PutAsJsonAsync($"/api/specs/{created.Id}", new
        {
            specCode = code,
            specName = "测试规格书-更新",
            parameters = new[] { new { key = "range", value = "0~2.5MPa" } }
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content.ReadFromJsonAsync<EquipmentSpecDto>())!;
        Assert.Equal("测试规格书-更新", updated.SpecName);
        Assert.Equal("0~2.5MPa", Assert.Single(updated.Parameters!).Value);
        Assert.Equal(1, updated.Version);

        var filtered = await Client.GetFromJsonAsync<PagedResult<EquipmentSpecDto>>(
            $"/api/projects/{project.Id}/specs?equipmentId={equipment.Id}");
        Assert.Contains(filtered!.Items, x => x.Id == created.Id);

        // 设备删除先被规格书阻塞。
        var blockedDelete = await Client.DeleteAsync($"/api/equipments/{equipment.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, blockedDelete.StatusCode);

        var deleteSpec = await Client.DeleteAsync($"/api/specs/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteSpec.StatusCode);

        // 规格书删除后，设备删除恢复。
        var deleteEquipment = await Client.DeleteAsync($"/api/equipments/{equipment.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteEquipment.StatusCode);
    }

    [Fact]
    public async Task Spec_NullParameterRow_ReturnsBadRequest()
    {
        await LoginAsync();
        var project = await GetDemoProjectAsync();
        var equipment = await CreateEquipmentAsync(project.Id);

        try
        {
            var response = await Client.PostAsJsonAsync($"/api/equipments/{equipment.Id}/specs", new
            {
                specCode = "SPEC-" + Guid.NewGuid().ToString("N")[..8],
                specName = "空参数行",
                parameters = new object?[] { null }
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await Client.DeleteAsync($"/api/equipments/{equipment.Id}");
        }
    }

    [Fact]
    public async Task Spec_DuplicateParameterKeys_ReturnsBadRequest()
    {
        await LoginAsync();
        var project = await GetDemoProjectAsync();
        var equipment = await CreateEquipmentAsync(project.Id);

        try
        {
            var response = await Client.PostAsJsonAsync($"/api/equipments/{equipment.Id}/specs", new
            {
                specCode = "SPEC-" + Guid.NewGuid().ToString("N")[..8],
                specName = "坏参数",
                parameters = new[] { new { key = "dup", value = "a" }, new { key = "DUP", value = "b" } }
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await Client.DeleteAsync($"/api/equipments/{equipment.Id}");
        }
    }

    [Fact]
    public async Task Viewer_CanReadButCannotWriteSpecs()
    {
        await LoginAsync("viewer", "viewer123");
        var project = await GetDemoProjectAsync();

        var list = await Client.GetFromJsonAsync<PagedResult<EquipmentDto>>(
            $"/api/projects/{project.Id}/equipments?search=PT-1001");
        var equipment = list!.Items.Single(x => x.TagNo == "PT-1001");

        var create = await Client.PostAsJsonAsync($"/api/equipments/{equipment.Id}/specs", new
        {
            specCode = "SPEC-VIEWER-" + Guid.NewGuid().ToString("N")[..6],
            specName = "只读账号不能创建",
            parameters = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        var specs = await Client.GetFromJsonAsync<PagedResult<EquipmentSpecDto>>(
            $"/api/projects/{project.Id}/specs");
        Assert.NotNull(specs);
    }

    [Fact]
    public async Task ManagementToken_CannotAccessSpecs()
    {
        await LoginManagementAsync();

        var response = await Client.GetAsync($"/api/projects/{Guid.NewGuid()}/specs");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<EquipmentDto> CreateEquipmentAsync(Guid projectId)
    {
        var response = await Client.PostAsJsonAsync($"/api/projects/{projectId}/equipments", new
        {
            tagNo = "TEST-" + Guid.NewGuid().ToString("N")[..8],
            name = "规格书测试设备",
            typeCode = "PT",
            discipline = "Instrument",
            location = "",
            description = ""
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EquipmentDto>())!;
    }
}
