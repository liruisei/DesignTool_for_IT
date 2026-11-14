using WinDesign.Client.Services;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Telecom;
using WinDesign.DesignClient.ViewModels;

namespace WinDesign.Client.Tests;

public class TelecomStatsViewModelTests
{
    [Fact]
    public async Task Stats_GroupByBuildingAndFloor_AndCalculateSwitchCounts()
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

        Assert.Equal(2, viewModel.BuildingSummaries.Count);
        Assert.Equal(3, viewModel.FloorSummaries.Count);

        // 汇总中的建筑物列只显示编号，不显示名称。
        Assert.Contains(viewModel.BuildingSummaries, x => x.Building == "B1");
        Assert.Contains(viewModel.BuildingSummaries, x => x.Building == "B2");
        Assert.DoesNotContain(viewModel.BuildingSummaries, x => x.Building.Contains("综合楼"));

        var floor1 = viewModel.FloorSummaries.Single(x => x.Building.Contains("B1") && x.Floor == "1F");
        Assert.Equal(1, floor1.RoomCount);
        Assert.Equal(100, floor1.DataPointCount);
        Assert.Equal(50, floor1.PhonePointCount);
        Assert.Equal(10, floor1.ApCount);
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
            Guid.NewGuid(), "SW-24A", "24 口接入", "Access", 24, 4, true, 1, null));
        api.SwitchModels.Add(new SwitchModelDto(
            Guid.NewGuid(), "SW-48A", "48 口接入", "Access", 48, 4, true, 2, null));
        api.SwitchModels.Add(new SwitchModelDto(
            Guid.NewGuid(), "SW-24P", "24 口 PoE", "PoE", 24, 4, true, 3, null));
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
