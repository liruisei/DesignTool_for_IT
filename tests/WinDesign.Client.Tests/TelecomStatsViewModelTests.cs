using WinDesign.Client.Services;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Telecom;
using WinDesign.Contracts.Workspaces;
using WinDesign.DesignClient.ViewModels;

namespace WinDesign.Client.Tests;

public class TelecomStatsViewModelTests
{
    [Fact]
    public async Task Stats_AggregateCounts_AndCalculateSwitchCounts()
    {
        var api = new FakeApiClient();
        var (building1, building2, _) = AddTelecomPbs(api);
        AddSwitchModels(api);
        AddRoom(api, building1, "1F", "R101", 100, 50, 10);
        AddRoom(api, building1, "2F", "R201", 20, 0, 0);
        AddRoom(api, building2, "1F", "R102", 0, 60, 0);

        var viewModel = CreateViewModel(api);
        await viewModel.RefreshAsync();

        Assert.Equal(2, viewModel.BuildingCount);
        Assert.Equal(3, viewModel.FloorCount);
        Assert.Equal(3, viewModel.RoomCount);
        Assert.Equal(120, viewModel.DataPointTotal);
        Assert.Equal(110, viewModel.PhonePointTotal);
        Assert.Equal(10, viewModel.ApTotal);

        // 电话点不占用交换机电口：接入交换机只按 120 个信息点计算。
        // 24 口 * 80% 可用 = 19.2 -> ceil(120 / 19.2) = 7
        Assert.Equal(7, viewModel.AccessSwitchTotal);
        // 10 个 AP / 19.2 -> 1 台 PoE
        Assert.Equal(1, viewModel.PoeSwitchTotal);

        // 计算结果区显示所选样本的型号与端口数。
        Assert.Equal("SW-24A · 24 口", viewModel.AccessSwitchHint);
        Assert.Equal("SW-24P · 24 口", viewModel.PoeSwitchHint);

        // 台数按建筑物分行，B2 只有电话点，不需要交换机。
        Assert.Equal(2, viewModel.BuildingSwitchTotals.Count);
        var b1 = viewModel.BuildingSwitchTotals.Single(x => x.Building == "B1");
        Assert.Equal(120, b1.DataPointCount);
        Assert.Equal(10, b1.ApCount);
        Assert.Equal(7, b1.AccessSwitchCount);
        Assert.Equal(1, b1.PoeSwitchCount);
        var b2 = viewModel.BuildingSwitchTotals.Single(x => x.Building == "B2");
        Assert.Equal(0, b2.AccessSwitchCount);
        Assert.Equal(0, b2.PoeSwitchCount);
    }

    [Fact]
    public async Task SwitchCounts_AreRoundedPerBuilding_NotAcrossBuildings()
    {
        var api = new FakeApiClient();
        var (building1, building2, _) = AddTelecomPbs(api);
        AddSwitchModels(api);
        AddRoom(api, building1, "1F", "R101", 10, 0, 10);
        AddRoom(api, building2, "1F", "R201", 5, 0, 5);

        var viewModel = CreateViewModel(api);
        await viewModel.RefreshAsync();

        // 合计 15 个信息点若跨楼合并只需 1 台，但每栋楼各自取整后是 2 台。
        Assert.Equal(2, viewModel.AccessSwitchTotal);
        Assert.Equal(2, viewModel.PoeSwitchTotal);
        Assert.All(viewModel.BuildingSwitchTotals,
            x => Assert.Equal(1, x.AccessSwitchCount));
        Assert.All(viewModel.BuildingSwitchTotals,
            x => Assert.Equal(1, x.PoeSwitchCount));
    }

    [Fact]
    public async Task SwitchingAccessModel_RecalculatesTotal()
    {
        var api = new FakeApiClient();
        var (building, _, _) = AddTelecomPbs(api);
        AddSwitchModels(api);
        AddRoom(api, building, "1F", "R101", 50, 0, 0);

        var viewModel = CreateViewModel(api);
        await viewModel.RefreshAsync();

        Assert.Equal(24, viewModel.SelectedAccessSwitch!.ElectricalPorts);
        Assert.Equal(3, viewModel.AccessSwitchTotal); // ceil(50 / 19.2)

        viewModel.SelectedAccessSwitch = viewModel.AccessSwitchModels
            .Single(x => x.ElectricalPorts == 48);
        Assert.Equal(2, viewModel.AccessSwitchTotal); // ceil(50 / 38.4)
    }

    [Fact]
    public async Task SwitchSelectionAndUtilization_AreSharedThroughSettings()
    {
        var api = new FakeApiClient();
        var (building, _, _) = AddTelecomPbs(api);
        AddSwitchModels(api);
        AddRoom(api, building, "1F", "R101", 10, 0, 0);

        var settings = new TelecomSwitchSettings();
        var viewModel = new TelecomStatsViewModel(
            api.CreateSession(),
            new ProjectContext { CurrentProject = api.Project },
            settings);
        await viewModel.RefreshAsync();

        // 信息设计页选中的样本与可用率写回共享设置，索引设计页据此只读展示。
        Assert.Equal(viewModel.SelectedAccessSwitch!.Id, settings.AccessSwitchId);
        Assert.Equal(viewModel.SelectedPoeSwitch!.Id, settings.PoeSwitchId);

        viewModel.UtilizationPercent = 50;
        Assert.Equal(50, settings.UtilizationPercent);
    }

    [Fact]
    public async Task BuildingOptions_OnlyIncludeProjectPbsScope()
    {
        var api = new FakeApiClient();
        var (inside, _, outside) = AddTelecomPbs(api);

        var viewModel = CreateViewModel(api);
        await viewModel.RefreshAsync();

        var insideOption = Assert.Single(viewModel.BuildingOptions, x => x.Id == inside.Id);
        // 下拉只显示建筑物编号，不拼接名称。
        Assert.Equal(inside.Code, insideOption.Display);
        Assert.DoesNotContain(viewModel.BuildingOptions, x => x.Id == outside.Id);
    }

    [Fact]
    public async Task AddRoom_DefaultsToWorkspaceUnit_AndChangingUnitFiltersBuildings()
    {
        var api = new FakeApiClient();
        var root = api.AddDefaultPbsRoot();
        var unitA = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, root.Id, "1279", "装置 1279", "Unit", 1, null, 0, 0);
        var unitB = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, root.Id, "1885", "装置 1885", "Unit", 2, null, 0, 0);
        var a1 = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, unitA.Id, "FAR2-01", "建筑一", "Building", 1, null, 0, 0);
        var a2 = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, unitA.Id, "FAR2-02", "建筑二", "Building", 2, null, 0, 0);
        var b1 = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, unitB.Id, "FAR3-01", "建筑三", "Building", 1, null, 0, 0);
        api.PbsNodes.AddRange(new[] { unitA, unitB, a1, a2, b1 });

        var session = api.CreateSession();
        // 登录工作区时选择的默认装置应带出到新增行。
        session.SetSelectedUnit(new WorkspaceUnitDto(unitB.Id, unitB.Code, unitB.Name));
        var viewModel = new TelecomStatsViewModel(
            session, new ProjectContext { CurrentProject = api.Project });
        await viewModel.RefreshAsync();

        viewModel.AddRoomCommand.Execute(null);
        var row = viewModel.SelectedRoom!;

        Assert.Equal(unitB.Id, row.UnitId);
        Assert.Equal(new[] { "FAR3-01" }, row.BuildingOptions.Select(x => x.Display));
        Assert.Equal(b1.Id, row.PbsNodeId);
        // 模板列的排序/筛选依据：装置与建筑物的编码字符串。
        Assert.Equal("1885", row.UnitCode);
        Assert.Equal("FAR3-01", row.BuildingCode);

        // 切换到 1279 后只能选到该装置下的建筑物。
        row.UnitId = unitA.Id;
        Assert.Equal(new[] { "FAR2-01", "FAR2-02" }, row.BuildingOptions.Select(x => x.Display));
        Assert.Equal(a1.Id, row.PbsNodeId);
        Assert.Equal("1279", row.UnitCode);
        Assert.Equal("FAR2-01", row.BuildingCode);
        Assert.DoesNotContain(row.BuildingOptions, x => x.Display == "FAR3-01");
    }

    [Fact]
    public async Task LoadedRows_ShowUnitDerivedFromBuilding()
    {
        var api = new FakeApiClient();
        var root = api.AddDefaultPbsRoot();
        var unit = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, root.Id, "U1", "装置一", "Unit", 1, null, 0, 0);
        var building1 = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, unit.Id, "B1", "综合楼", "Building", 1, null, 0, 0);
        var building2 = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, unit.Id, "B2", "机柜间", "Building", 2, null, 0, 0);
        api.PbsNodes.AddRange(new[] { unit, building1, building2 });
        AddSwitchModels(api);
        AddRoom(api, building2, "1F", "R101", 10, 0, 0);

        var viewModel = CreateViewModel(api);
        await viewModel.RefreshAsync();

        var row = Assert.Single(viewModel.Rooms);
        Assert.Equal(unit.Id, row.UnitId);
        Assert.Equal(building2.Id, row.PbsNodeId);
        Assert.Equal(new[] { "B1", "B2" }, row.BuildingOptions.Select(x => x.Display));
    }

    [Fact]
    public async Task DeleteRowCommand_OnMultiSelection_DeletesAllSelectedRows()
    {
        var api = new FakeApiClient();
        var (building, _, _) = AddTelecomPbs(api);
        AddSwitchModels(api);
        AddRoom(api, building, "1F", "R101", 10, 0, 0);
        AddRoom(api, building, "1F", "R102", 20, 0, 0);
        AddRoom(api, building, "1F", "R103", 30, 0, 0);

        var viewModel = CreateViewModel(api);
        await viewModel.RefreshAsync();
        Assert.Equal(3, viewModel.Rooms.Count);

        // 右键落在已选中的行上：删除整组选中行。
        viewModel.SetSelection(new[] { viewModel.Rooms[0], viewModel.Rooms[2] });
        await viewModel.DeleteRowCommand.ExecuteAsync(viewModel.Rooms[0]);

        var remaining = Assert.Single(viewModel.Rooms);
        Assert.Equal("R102", remaining.Code);
        Assert.Equal("R102", Assert.Single(api.TelecomRooms).Code);
    }

    [Fact]
    public async Task DeleteRowCommand_RemovesOnlyThatRow()
    {
        var api = new FakeApiClient();
        var (building, _, _) = AddTelecomPbs(api);
        AddSwitchModels(api);
        AddRoom(api, building, "1F", "R101", 10, 0, 0);
        AddRoom(api, building, "1F", "R102", 20, 0, 0);

        var viewModel = CreateViewModel(api);
        await viewModel.RefreshAsync();

        // 单选（或右键未选中的行）时只删被右键的那一行。
        viewModel.SetSelection(new[] { viewModel.Rooms[0] });
        await viewModel.DeleteRowCommand.ExecuteAsync(viewModel.Rooms[0]);

        Assert.Equal("R102", Assert.Single(viewModel.Rooms).Code);
        Assert.Equal("R102", Assert.Single(api.TelecomRooms).Code);
    }

    [Fact]
    public async Task DeleteMenuHeader_ReflectsSelectionCount()
    {
        var api = new FakeApiClient();
        var (building, _, _) = AddTelecomPbs(api);
        AddSwitchModels(api);
        AddRoom(api, building, "1F", "R101", 10, 0, 0);
        AddRoom(api, building, "1F", "R102", 20, 0, 0);

        var viewModel = CreateViewModel(api);
        await viewModel.RefreshAsync();

        viewModel.SetSelection(new[] { viewModel.Rooms[0] });
        Assert.Equal("删除", viewModel.DeleteMenuHeader);

        viewModel.SetSelection(new[] { viewModel.Rooms[0], viewModel.Rooms[1] });
        Assert.Equal("删除选中的 2 行", viewModel.DeleteMenuHeader);
    }

    [Fact]
    public async Task Delete_WithoutSelection_ShowsError()
    {
        var api = new FakeApiClient();
        var (building, _, _) = AddTelecomPbs(api);
        AddSwitchModels(api);
        AddRoom(api, building, "1F", "R101", 10, 0, 0);

        var viewModel = CreateViewModel(api);
        await viewModel.RefreshAsync();

        await viewModel.DeleteRowCommand.ExecuteAsync(null);

        Assert.NotNull(viewModel.ErrorMessage);
        Assert.Single(viewModel.Rooms);
    }

    [Fact]
    public async Task Save_AllowsEmptyRoomName()
    {
        var api = new FakeApiClient();
        var (building, _, _) = AddTelecomPbs(api);
        AddSwitchModels(api);

        var viewModel = CreateViewModel(api);
        await viewModel.RefreshAsync();

        viewModel.AddRoomCommand.Execute(null);
        viewModel.SelectedRoom!.Code = "R-EMPTY";
        viewModel.SelectedRoom.Name = string.Empty;

        await viewModel.SaveChangesCommand.ExecuteAsync(null);

        Assert.Null(viewModel.ErrorMessage);
        var saved = Assert.Single(api.TelecomRooms);
        Assert.Equal("R-EMPTY", saved.Code);
        Assert.Equal(string.Empty, saved.Name);
        Assert.Equal(building.Id, saved.PbsNodeId);
    }

    [Fact]
    public async Task SavePending_UpdatesExistingRowsOnly()
    {
        var api = new FakeApiClient();
        var (building, _, _) = AddTelecomPbs(api);
        AddSwitchModels(api);
        AddRoom(api, building, "1F", "R101", 10, 0, 0);

        var viewModel = CreateViewModel(api);
        await viewModel.RefreshAsync();

        viewModel.Rooms[0].DataPointCount = 30;
        await viewModel.SavePendingAsync();

        Assert.Equal(30, api.TelecomRooms.Single().DataPointCount);
        Assert.Single(api.TelecomRooms);
    }

    private static TelecomStatsViewModel CreateViewModel(FakeApiClient api) =>
        new(api.CreateSession(), new ProjectContext { CurrentProject = api.Project });

    private static (PbsNodeDto Inside1, PbsNodeDto Inside2, PbsNodeDto Outside) AddTelecomPbs(
        FakeApiClient api)
    {
        var root = api.AddDefaultPbsRoot();
        var unit = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, root.Id, "U1", "仪表装置", "Unit", 1, null, 0, 0);
        var inside1 = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, unit.Id, "B1", "综合楼", "Building", 1, null, 0, 0);
        var inside2 = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, unit.Id, "B2", "机柜间", "Building", 2, null, 0, 0);

        var outsideArea = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, null, "AREA-X", "范围外区域", "Area", 9, null, 0, 0);
        var outside = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, outsideArea.Id, "B-X", "范围外建筑", "Building", 1, null, 0, 0);

        api.PbsNodes.AddRange(new[] { unit, inside1, inside2, outsideArea, outside });
        return (inside1, inside2, outside);
    }

    private static void AddSwitchModels(FakeApiClient api)
    {
        api.SwitchModels.Add(new SwitchModelDto(
            Guid.NewGuid(), "SW-24A", "24 口接入", "Telecom", "SW", "交换机", 24, 4, true, 1, null));
        api.SwitchModels.Add(new SwitchModelDto(
            Guid.NewGuid(), "SW-48A", "48 口接入", "Telecom", "SW", "交换机", 48, 4, true, 2, null));
        api.SwitchModels.Add(new SwitchModelDto(
            Guid.NewGuid(), "SW-24P", "24 口 PoE", "Telecom", "POE", "PoE 交换机", 24, 4, true, 3, null));
    }

    private static void AddRoom(
        FakeApiClient api,
        PbsNodeDto building,
        string floor,
        string code,
        int dataPoints,
        int phonePoints,
        int aps)
    {
        api.TelecomRooms.Add(new TelecomRoomDto(
            Guid.NewGuid(),
            api.Project.Id,
            building.Id,
            building.Code,
            building.Name,
            floor,
            code,
            "房间 " + code,
            dataPoints,
            phonePoints,
            aps,
            null,
            0));
    }
}
