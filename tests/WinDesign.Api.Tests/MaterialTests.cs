using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using WinDesign.Contracts.Common;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Materials;
using WinDesign.Contracts.Workspaces;

namespace WinDesign.Api.Tests;

public class MaterialTests : ApiTestBase
{
    public MaterialTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task SeedMaterial_VisibleOnlyInItsProject()
    {
        await LoginAsync();
        var project = await GetDemoProjectAsync();

        var list = await Client.GetFromJsonAsync<PagedResult<MaterialItemDto>>(
            $"/api/projects/{project.Id}/materials?search=MAT-001");
        var material = Assert.Single(list!.Items, x => x.Code == "MAT-001");
        Assert.Equal("PT-1001", material.EquipmentTagNo);
        Assert.Equal(120m, material.Quantity);

        // 同一工厂的 DEMO-002 不应看到属于 DEMO-001 的材料。
        var otherProject = await LoginDemo002Async();
        var otherList = await Client.GetFromJsonAsync<PagedResult<MaterialItemDto>>(
            $"/api/projects/{otherProject}/materials?search=MAT-001");
        Assert.DoesNotContain(otherList!.Items, x => x.Code == "MAT-001");
    }

    [Fact]
    public async Task Material_Crud_And_UnblocksEquipmentDelete()
    {
        await LoginAsync();
        var project = await GetDemoProjectAsync();
        var equipment = await CreateEquipmentAsync(project.Id);
        var code = "MAT-" + Guid.NewGuid().ToString("N")[..8];

        var create = await Client.PostAsJsonAsync($"/api/projects/{project.Id}/materials", new
        {
            equipmentId = equipment.Id,
            code,
            name = "测试材料",
            spec = "Φ14×2",
            material = "316SS",
            unit = "米",
            quantity = 12.5m,
            remark = "integration test"
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<MaterialItemDto>())!;
        Assert.Equal(equipment.TagNo, created.EquipmentTagNo);
        Assert.Equal(12.5m, created.Quantity);

        var duplicate = await Client.PostAsJsonAsync($"/api/projects/{project.Id}/materials", new
        {
            code,
            name = "重复材料",
            unit = "个",
            quantity = 1m
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var unknownEquipment = await Client.PostAsJsonAsync($"/api/projects/{project.Id}/materials", new
        {
            equipmentId = Guid.NewGuid(),
            code = code + "-X",
            name = "无效设备材料",
            unit = "个",
            quantity = 1m
        });
        Assert.Equal(HttpStatusCode.BadRequest, unknownEquipment.StatusCode);

        var update = await Client.PutAsJsonAsync($"/api/materials/{created.Id}", new
        {
            equipmentId = equipment.Id,
            code,
            name = "测试材料-更新",
            spec = "Φ16×2",
            material = "304SS",
            unit = "米",
            quantity = 20m,
            remark = "updated"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content.ReadFromJsonAsync<MaterialItemDto>())!;
        Assert.Equal("测试材料-更新", updated.Name);
        Assert.Equal(20m, updated.Quantity);

        var blockedDelete = await Client.DeleteAsync($"/api/equipments/{equipment.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, blockedDelete.StatusCode);

        var deleteMaterial = await Client.DeleteAsync($"/api/materials/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteMaterial.StatusCode);

        var deleteEquipment = await Client.DeleteAsync($"/api/equipments/{equipment.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteEquipment.StatusCode);
    }

    [Fact]
    public async Task Viewer_CanReadButCannotWriteMaterials()
    {
        await LoginAsync("viewer", "viewer123");
        var project = await GetDemoProjectAsync();

        var create = await Client.PostAsJsonAsync($"/api/projects/{project.Id}/materials", new
        {
            code = "MAT-VIEWER",
            name = "只读账号不能创建",
            unit = "个",
            quantity = 1m
        });
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);

        var list = await Client.GetFromJsonAsync<PagedResult<MaterialItemDto>>(
            $"/api/projects/{project.Id}/materials");
        Assert.NotNull(list);
    }

    [Fact]
    public async Task ManagementToken_CannotAccessMaterials()
    {
        await LoginManagementAsync();

        var response = await Client.GetAsync($"/api/projects/{Guid.NewGuid()}/materials");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<Guid> LoginDemo002Async()
    {
        await LoginIdentityAsync();
        var workspaces = await Client.GetFromJsonAsync<List<WorkspacePlantDto>>("/api/auth/workspaces");
        var plant = workspaces!.Single(x => x.Projects.Any(p => p.Code == "DEMO-002"));
        var project = plant.Projects.Single(x => x.Code == "DEMO-002");

        var select = await Client.PostAsJsonAsync("/api/auth/workspace", new
        {
            plantId = plant.Id,
            projectId = project.Id
        });
        select.EnsureSuccessStatusCode();
        var workspace = (await select.Content.ReadFromJsonAsync<WorkspaceResponse>())!;
        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", workspace.AccessToken);
        return project.Id;
    }

    private async Task<EquipmentDto> CreateEquipmentAsync(Guid projectId)
    {
        var response = await Client.PostAsJsonAsync($"/api/projects/{projectId}/equipments", new
        {
            tagNo = "TEST-" + Guid.NewGuid().ToString("N")[..8],
            name = "材料测试设备",
            typeCode = "PT",
            discipline = "Instrument",
            location = "",
            description = ""
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EquipmentDto>())!;
    }
}
