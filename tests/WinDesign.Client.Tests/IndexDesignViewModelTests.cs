using WinDesign.Client.Services;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Telecom;
using WinDesign.DesignClient.ViewModels;

namespace WinDesign.Client.Tests;

public class IndexDesignViewModelTests
{
    [Fact]
    public async Task ReadOnlyPanel_ReflectsSharedSettingsAndPerBuildingTotals()
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
        AddRoom(api, building1, "1F", "R1", 10, 0, 10);
        AddRoom(api, building2, "1F", "R2", 5, 0, 5);

        var settings = new TelecomSwitchSettings();
        var viewModel = new IndexDesignViewModel(
            api.CreateSession(),
            new ProjectContext { CurrentProject = api.Project },
            settings);
        await viewModel.RefreshAsync();

        // 未在信息设计页选择时回退到第一个样本；两栋楼各自 1 台。
        Assert.Equal(2, viewModel.AccessSwitchTotal);
        Assert.Equal(2, viewModel.PoeSwitchTotal);
        Assert.Equal(2, viewModel.BuildingSwitchTotals.Count);
        Assert.Contains("SW-24A", viewModel.AccessSwitchText);
        Assert.Equal("80%", viewModel.UtilizationText);

        // 信息设计页调整可用率后，索引设计只读面板即时刷新。
        settings.UtilizationPercent = 50;

        Assert.Equal("50%", viewModel.UtilizationText);
        Assert.Equal(2, viewModel.AccessSwitchTotal);
    }

    [Fact]
    public async Task IndexTable_IsIndependentEditableModule()
    {
        var api = new FakeApiClient();
        var viewModel = new IndexDesignViewModel(
            api.CreateSession(),
            new ProjectContext { CurrentProject = api.Project },
            new TelecomSwitchSettings());

        await viewModel.RefreshAsync();
        await viewModel.IndexTable.RefreshAsync();

        Assert.Equal("索引设计", viewModel.Title);
        Assert.Empty(viewModel.IndexTable.Equipments);
    }

    [Fact]
    public async Task PerBuildingRemaining_SubtractsOnlySwitchesInThatBuilding()
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
        AddRoom(api, building1, "1F", "R1", 10, 0, 10);
        AddRoom(api, building2, "1F", "R2", 10, 0, 10);

        api.EquipmentTypes.Add(new DictionaryItemDto(
            Guid.NewGuid(), Guid.NewGuid(), "EQUIPMENT_TYPE", "SW", "接入交换机", 1, true, null));
        api.EquipmentTypes.Add(new DictionaryItemDto(
            Guid.NewGuid(), Guid.NewGuid(), "EQUIPMENT_TYPE", "SWP", "PoE 交换机", 2, true, null));

        // B1 已建 2 台接入 + 1 台 PoE；B2 尚未建。
        AddEquipment(api, "SW-1001", "SW", building1.Id);
        AddEquipment(api, "SW-1002", "SW", building1.Id);
        AddEquipment(api, "SWP-1001", "SWP", building1.Id);

        var viewModel = new IndexDesignViewModel(
            api.CreateSession(),
            new ProjectContext { CurrentProject = api.Project },
            new TelecomSwitchSettings());
        await viewModel.RefreshAsync();

        Assert.Equal(2, viewModel.AccessSwitchTotal);
        Assert.Equal(2, viewModel.PoeSwitchTotal);
        Assert.Equal(0, viewModel.AccessSwitchRemaining);
        Assert.Equal(1, viewModel.PoeSwitchRemaining);

        var b1 = viewModel.BuildingSwitchTotals.Single(x => x.Building == "B1");
        var b2 = viewModel.BuildingSwitchTotals.Single(x => x.Building == "B2");
        Assert.Equal(-1, b1.AccessSwitchRemaining);
        Assert.Equal(0, b1.PoeSwitchRemaining);
        Assert.Equal(1, b2.AccessSwitchRemaining);
        Assert.Equal(1, b2.PoeSwitchRemaining);
    }

    [Fact]
    public async Task RemainingCounts_SubtractCreatedSwitches_AndAllowNegative()
    {
        var api = new FakeApiClient();
        var root = api.AddDefaultPbsRoot();
        var unit = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, root.Id, "U1", "装置一", "Unit", 1, null, 0, 0);
        var building = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, unit.Id, "B1", "综合楼", "Building", 1, null, 0, 0);
        api.PbsNodes.AddRange(new[] { unit, building });
        AddSwitchModels(api);
        AddRoom(api, building, "1F", "R1", 10, 0, 10);

        api.EquipmentTypes.Add(new DictionaryItemDto(
            Guid.NewGuid(), Guid.NewGuid(), "EQUIPMENT_TYPE", "SW", "接入交换机", 1, true, null));
        api.EquipmentTypes.Add(new DictionaryItemDto(
            Guid.NewGuid(), Guid.NewGuid(), "EQUIPMENT_TYPE", "SWP", "PoE 交换机", 2, true, null));

        // 计算结果各 1 台；已建 2 台接入、1 台 PoE → 接入剩余 -1（冗余），PoE 剩余 0。
        AddEquipment(api, "SW-1001", "SW");
        AddEquipment(api, "SW-1002", "SW");
        AddEquipment(api, "SWP-1001", "SWP");

        var viewModel = new IndexDesignViewModel(
            api.CreateSession(),
            new ProjectContext { CurrentProject = api.Project },
            new TelecomSwitchSettings());
        await viewModel.RefreshAsync();

        Assert.Equal(1, viewModel.AccessSwitchTotal);
        Assert.Equal(1, viewModel.PoeSwitchTotal);
        Assert.Equal(-1, viewModel.AccessSwitchRemaining);
        Assert.Equal(0, viewModel.PoeSwitchRemaining);
        Assert.Contains("-1", viewModel.AccessSwitchRemainingText);
    }

    private static void AddEquipment(
        FakeApiClient api,
        string tagNo,
        string typeCode,
        Guid? pbsNodeId = null) =>
        api.Equipments.Add(new EquipmentDto
        {
            Id = Guid.NewGuid(),
            PlantId = api.Plant.Id,
            ProjectId = api.Project.Id,
            PbsNodeId = pbsNodeId,
            TagNo = tagNo,
            Name = "交换机",
            TypeCode = typeCode,
            Discipline = "Telecom",
            Quantity = 1,
            CreatedAt = DateTime.UtcNow
        });

    private static void AddSwitchModels(FakeApiClient api)
    {
        api.SwitchModels.Add(new SwitchModelDto(
            Guid.NewGuid(), "SW-24A", "24 口接入", "Telecom", "SW", "交换机", 24, 4, true, 1, null));
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
