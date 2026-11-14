using System.Net;
using System.Net.Http.Json;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Telecom;

namespace WinDesign.Api.Tests;

public class TelecomTests : ApiTestBase
{
    public TelecomTests(ApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task TelecomRoom_Crud_RoundTrip()
    {
        var building = await CreateBuildingAsync();
        await LoginAsync();
        var projectId = Workspace.Project.Id;

        var createResponse = await Client.PostAsJsonAsync(
            $"/api/projects/{projectId}/telecom-rooms", new
            {
                pbsNodeId = building.Id,
                floor = "3F",
                code = "R301",
                name = "调度室",
                dataPointCount = 40,
                phonePointCount = 8,
                apCount = 2,
                remark = "测试房间",
                sortOrder = 1
            });
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<TelecomRoomDto>();
        Assert.NotNull(created);
        Assert.Equal(building.Code, created!.BuildingCode);
        Assert.Equal("调度室", created.Name);

        var rooms = await Client.GetFromJsonAsync<List<TelecomRoomDto>>(
            $"/api/projects/{projectId}/telecom-rooms");
        Assert.Contains(rooms!, x => x.Id == created.Id);

        var update = await Client.PutAsJsonAsync($"/api/telecom-rooms/{created.Id}", new
        {
            pbsNodeId = building.Id,
            floor = "3F",
            code = "R301",
            name = "调度室-改",
            dataPointCount = 50,
            phonePointCount = 8,
            apCount = 3,
            remark = (string?)null,
            sortOrder = 2
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<TelecomRoomDto>();
        Assert.Equal(50, updated!.DataPointCount);
        Assert.Equal("调度室-改", updated.Name);

        var delete = await Client.DeleteAsync($"/api/telecom-rooms/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task TelecomRoom_BuildingOutsideProjectScope_ReturnsBadRequest()
    {
        var outside = await CreateOutsideBuildingAsync();
        await LoginAsync();

        var response = await Client.PostAsJsonAsync(
            $"/api/projects/{Workspace.Project.Id}/telecom-rooms", new
            {
                pbsNodeId = outside.Id,
                floor = "1F",
                code = "R-X",
                name = "范围外房间",
                dataPointCount = 1,
                phonePointCount = 0,
                apCount = 0,
                remark = (string?)null,
                sortOrder = 0
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TelecomRoom_EmptyName_IsAllowed()
    {
        var building = await CreateBuildingAsync();
        await LoginAsync();

        var response = await Client.PostAsJsonAsync(
            $"/api/projects/{Workspace.Project.Id}/telecom-rooms", new
            {
                pbsNodeId = building.Id,
                floor = "1F",
                code = "R-NO-NAME",
                name = string.Empty,
                dataPointCount = 5,
                phonePointCount = 0,
                apCount = 0,
                remark = (string?)null,
                sortOrder = 0
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<TelecomRoomDto>();
        Assert.Equal(string.Empty, created!.Name);

        await Client.DeleteAsync($"/api/telecom-rooms/{created.Id}");
    }

    [Fact]
    public async Task SwitchModels_SeededForDesignAndAdminCanCrud()
    {
        await LoginAsync();
        var models = await Client.GetFromJsonAsync<List<SwitchModelDto>>(
            "/api/switch-models?activeOnly=true");
        Assert.NotNull(models);
        Assert.Contains(models!, x => x.SwitchType == "Access");
        Assert.Contains(models!, x => x.SwitchType == "PoE");

        await LoginManagementAsync();
        var code = "SW-T-" + Guid.NewGuid().ToString("N")[..6];
        var create = await Client.PostAsJsonAsync("/api/switch-models", new
        {
            code,
            name = "测试接入交换机",
            switchType = "Access",
            electricalPorts = 48,
            uplinkPorts = 4,
            isActive = true,
            sortOrder = 900,
            remark = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<SwitchModelDto>();
        Assert.NotNull(created);
        Assert.Equal(48, created!.ElectricalPorts);

        var update = await Client.PutAsJsonAsync($"/api/switch-models/{created.Id}", new
        {
            code,
            name = "测试接入交换机-改",
            switchType = "Access",
            electricalPorts = 24,
            uplinkPorts = 2,
            isActive = true,
            sortOrder = 901,
            remark = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<SwitchModelDto>();
        Assert.Equal(24, updated!.ElectricalPorts);

        var delete = await Client.DeleteAsync($"/api/switch-models/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task DeviceSamples_IncludeCabinet_AndToggleActiveRoundTrips()
    {
        await LoginManagementAsync();
        var models = await Client.GetFromJsonAsync<List<SwitchModelDto>>("/api/switch-models");
        Assert.Contains(models!, x => x.SwitchType == "Cabinet");

        var access = models!.First(x => x.SwitchType == "Access");
        var toggle = await Client.PostAsync($"/api/switch-models/{access.Id}/toggle-active", null);
        Assert.Equal(HttpStatusCode.OK, toggle.StatusCode);
        var toggled = await toggle.Content.ReadFromJsonAsync<SwitchModelDto>();
        Assert.Equal(!access.IsActive, toggled!.IsActive);

        // 还原启用状态，避免影响其他用例。
        await Client.PostAsync($"/api/switch-models/{access.Id}/toggle-active", null);
    }

    [Fact]
    public async Task DeviceSample_CabinetAllowsZeroPorts_ButSwitchRequiresPorts()
    {
        await LoginManagementAsync();
        var cabinetCode = "CAB-T-" + Guid.NewGuid().ToString("N")[..6];

        var cabinet = await Client.PostAsJsonAsync("/api/switch-models", new
        {
            code = cabinetCode,
            name = "测试机柜",
            switchType = "Cabinet",
            electricalPorts = 0,
            uplinkPorts = (int?)null,
            isActive = true,
            sortOrder = 910,
            remark = (string?)null
        });
        Assert.Equal(HttpStatusCode.OK, cabinet.StatusCode);
        var cabinetDto = await cabinet.Content.ReadFromJsonAsync<SwitchModelDto>();

        var badSwitch = await Client.PostAsJsonAsync("/api/switch-models", new
        {
            code = "SW-T-" + Guid.NewGuid().ToString("N")[..6],
            name = "无端口交换机",
            switchType = "Access",
            electricalPorts = 0,
            uplinkPorts = 4,
            isActive = true,
            sortOrder = 911,
            remark = (string?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, badSwitch.StatusCode);

        if (cabinetDto is not null)
        {
            await Client.DeleteAsync($"/api/switch-models/{cabinetDto.Id}");
        }
    }

    [Fact]
    public async Task Designer_CannotWriteSwitchModels()
    {
        await LoginAsync("designer", "designer123");

        var response = await Client.PostAsJsonAsync("/api/switch-models", new
        {
            code = "SW-DENY-" + Guid.NewGuid().ToString("N")[..6],
            name = "不应创建",
            switchType = "Access",
            electricalPorts = 24,
            uplinkPorts = 4,
            isActive = true,
            sortOrder = 0,
            remark = (string?)null
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<PbsNodeDto> CreateBuildingAsync()
    {
        await LoginManagementAsync();
        var nodes = await Client.GetFromJsonAsync<List<PbsNodeDto>>($"/api/plants/{PlantId}/pbs");
        var unit = nodes!.First(x => x.Code == "CDU");
        return await CreatePbsNodeAsync(unit.Id, "Building");
    }

    private async Task<PbsNodeDto> CreateOutsideBuildingAsync()
    {
        await LoginManagementAsync();
        var area = await CreatePbsNodeAsync(null, "Area");
        var unit = await CreatePbsNodeAsync(area.Id, "Unit");
        return await CreatePbsNodeAsync(unit.Id, "Building");
    }

    private async Task<PbsNodeDto> CreatePbsNodeAsync(Guid? parentId, string nodeType)
    {
        var code = "T-" + nodeType[..2].ToUpperInvariant() + "-" +
                   Guid.NewGuid().ToString("N")[..6];
        var response = await Client.PostAsJsonAsync($"/api/plants/{PlantId}/pbs", new
        {
            parentId,
            code,
            name = "电信测试" + nodeType,
            nodeType,
            sortOrder = 950,
            description = ""
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PbsNodeDto>())!;
    }
}
