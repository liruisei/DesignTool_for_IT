using WinDesign.AdminClient.Services;
using WinDesign.Client.Services;
using WinDesign.AdminClient.ViewModels;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Plants;
using WinDesign.Contracts.Projects;

namespace WinDesign.Client.Tests;

public sealed class FakePbsDialogService : IPbsDialogService
{
    public PbsNodeEditorContext? LastContext { get; private set; }
    public PbsNodeEditorResult? NextResult { get; set; }
    public PlantEditorContext? LastPlantContext { get; private set; }
    public PlantEditorResult? NextPlantResult { get; set; }
    public bool ConfirmResult { get; set; } = true;

    public PbsNodeEditorResult? EditPbsNode(PbsNodeEditorContext context)
    {
        LastContext = context;
        return NextResult;
    }

    public PlantEditorResult? EditPlant(PlantEditorContext context)
    {
        LastPlantContext = context;
        return NextPlantResult;
    }

    public bool Confirm(string message) => ConfirmResult;
}

public class PbsViewModelTests
{
    [Fact]
    public async Task Refresh_BuildsHierarchyAndParentOptions()
    {
        var (api, project, area, unit, system) = CreateTree();
        var viewModel = CreateViewModel(api, project);

        await viewModel.RefreshAsync();

        Assert.Single(viewModel.Roots);
        Assert.True(viewModel.Roots[0].IsPlant);
        Assert.Single(viewModel.Roots[0].Children);
        Assert.Single(viewModel.Roots[0].Children[0].Children);
        Assert.Single(viewModel.Roots[0].Children[0].Children[0].Children);
        Assert.Contains(viewModel.ParentOptions, x => x.Id == system.Id);
        Assert.Contains(viewModel.ParentOptions, x => x.Id == unit.Id);
        Assert.Contains(viewModel.ParentOptions, x => x.Id == area.Id);
    }

    [Fact]
    public async Task NewChild_OpensEditorWithSelectedParent_AndCreatesNode()
    {
        var (api, project, area, _, _) = CreateTree();
        var dialogs = new FakePbsDialogService
        {
            NextResult = new PbsNodeEditorResult(area.Id, "NEW-CHILD", "新子节点", "System", 5, null)
        };
        var viewModel = CreateViewModel(api, project, dialogs);
        await viewModel.RefreshAsync();
        viewModel.SelectedNode = viewModel.Roots[0].Children[0];

        await viewModel.NewChildCommand.ExecuteAsync(null);

        Assert.NotNull(dialogs.LastContext);
        Assert.Equal(area.Id, dialogs.LastContext!.DefaultParentId);
        Assert.Equal("Unit", dialogs.LastContext.DefaultNodeType);
        Assert.Contains(api.PbsNodes, x => x.Code == "NEW-CHILD");
    }

    [Fact]
    public async Task NewSibling_OpensEditorWithSameParent_AndCreatesNode()
    {
        var (api, project, area, _, _) = CreateTree();
        var dialogs = new FakePbsDialogService
        {
            NextResult = new PbsNodeEditorResult(area.Id, "SIBLING", "同级节点", "Unit", 6, null)
        };
        var viewModel = CreateViewModel(api, project, dialogs);
        await viewModel.RefreshAsync();
        viewModel.SelectedNode = viewModel.Roots[0].Children[0].Children[0];

        await viewModel.NewSiblingCommand.ExecuteAsync(null);

        Assert.Equal(area.Id, dialogs.LastContext!.DefaultParentId);
        Assert.Equal("Unit", dialogs.LastContext.DefaultNodeType);
        Assert.Contains(api.PbsNodes, x => x.Code == "SIBLING");
    }

    [Fact]
    public async Task NewSibling_OnTopLevel_CreatesAnotherTopLevelNode()
    {
        var (api, project, _, _, _) = CreateTree();
        var dialogs = new FakePbsDialogService
        {
            NextResult = new PbsNodeEditorResult(null, "AREA-B", "B 区", "Area", 7, null)
        };
        var viewModel = CreateViewModel(api, project, dialogs);
        await viewModel.RefreshAsync();
        viewModel.SelectedNode = viewModel.Roots[0].Children[0];

        await viewModel.NewSiblingCommand.ExecuteAsync(null);

        Assert.Null(dialogs.LastContext!.DefaultParentId);
        Assert.Equal("Area", dialogs.LastContext.DefaultNodeType);
        Assert.Contains(api.PbsNodes, x => x.Code == "AREA-B" && x.ParentId is null);
    }

    [Fact]
    public async Task EditNode_UpdatesSelectedNode()
    {
        var (api, project, area, _, _) = CreateTree();
        var dialogs = new FakePbsDialogService
        {
            NextResult = new PbsNodeEditorResult(area.ParentId, area.Code, "改名后的节点", area.NodeType, area.SortOrder, null)
        };
        var viewModel = CreateViewModel(api, project, dialogs);
        await viewModel.RefreshAsync();
        viewModel.SelectedNode = viewModel.Roots[0].Children[0];

        await viewModel.EditNodeCommand.ExecuteAsync(null);

        Assert.Equal("改名后的节点", api.PbsNodes.Single(x => x.Id == area.Id).Name);
    }

    [Fact]
    public async Task DeleteNode_ConfirmsAndDeletes()
    {
        var (api, project, _, unit, _) = CreateTree();
        var dialogs = new FakePbsDialogService { ConfirmResult = true };
        var viewModel = CreateViewModel(api, project, dialogs);
        await viewModel.RefreshAsync();
        viewModel.SelectedNode = viewModel.Roots[0].Children[0].Children[0];

        await viewModel.DeleteNodeCommand.ExecuteAsync(null);

        Assert.DoesNotContain(api.PbsNodes, x => x.Id == unit.Id);
    }

    [Fact]
    public async Task Refresh_IncludesEquipmentUnderPbsNode()
    {
        var (api, project, _, _, system) = CreateTree();
        api.Equipments.Add(new EquipmentDto
        {
            Id = Guid.NewGuid(),
            PlantId = api.Plant.Id,
            ProjectId = project.Id,
            PbsNodeId = system.Id,
            TagNo = "PT-9001",
            Name = "设备",
            Discipline = "Instrument",
            Quantity = 1,
            CreatedAt = DateTime.UtcNow
        });
        var viewModel = CreateViewModel(api, project);

        await viewModel.RefreshAsync();

        var systemItem = viewModel.Roots[0].Children[0].Children[0].Children[0];
        Assert.Contains(systemItem.Children, x => x.Equipment?.TagNo == "PT-9001");
    }

    [Fact]
    public async Task Selection_DrivesContextMenuScope()
    {
        var (api, project, area, _, _) = CreateTree();
        var viewModel = CreateViewModel(api, project);
        await viewModel.RefreshAsync();

        // 默认选中工厂根节点：只能维护区域，不能维护工厂（工作区模式）。
        Assert.True(viewModel.IsFactoryNodeSelected);
        Assert.False(viewModel.IsPbsNodeSelected);
        Assert.False(viewModel.CanManageFactoriesHere);

        // 选中区域后：只提供节点操作，不再提供工厂操作。
        viewModel.SelectedNode = viewModel.Roots[0].Children[0];
        Assert.False(viewModel.IsFactoryNodeSelected);
        Assert.True(viewModel.IsPbsNodeSelected);
        Assert.False(viewModel.CanManageFactoriesHere);
        Assert.Equal(area.Id, viewModel.SelectedNode.Node!.Id);
    }

    [Fact]
    public async Task Refresh_ShowsFactoryAsPbsRoot()
    {
        var (api, project, _, _, _) = CreateTree();
        var viewModel = CreateViewModel(api, project);

        await viewModel.RefreshAsync();

        Assert.Single(viewModel.Roots);
        Assert.True(viewModel.Roots[0].IsPlant);
        Assert.Equal(api.Plant.Code, viewModel.Roots[0].HeaderCode);
        Assert.Equal(api.Plant.Name, viewModel.Roots[0].HeaderName);
    }

    [Fact]
    public async Task ManagementMode_ShowsAllPlants()
    {
        var api = new FakeApiClient();
        api.PlantDtos.Add(new PlantDto(Guid.NewGuid(), "PLANT-A", "A 厂", null, null, null, true, 1, 0, DateTime.UtcNow));
        api.PlantDtos.Add(new PlantDto(Guid.NewGuid(), "PLANT-B", "B 厂", null, null, null, true, 0, 0, DateTime.UtcNow));

        var viewModel = new PbsViewModel(
            api.CreateManagementSession(),
            new ProjectContext(),
            new FakePbsDialogService(),
            new FakeEquipmentDialogService());

        await viewModel.RefreshAsync();

        Assert.True(viewModel.IsManagementMode);
        Assert.True(viewModel.CanManagePlants);
        Assert.Equal(2, viewModel.Plants.Count);
        Assert.Equal(2, viewModel.Roots.Count);
        Assert.All(viewModel.Roots, x => Assert.True(x.IsPlant));
        Assert.All(viewModel.Roots, x => Assert.True(x.IsExpanded));
        Assert.True(viewModel.IsFactoryNodeSelected);
        Assert.True(viewModel.CanManageFactoriesHere);

        viewModel.CollapseAllCommand.Execute(null);
        Assert.All(viewModel.Roots, x => Assert.False(x.IsExpanded));

        viewModel.ExpandAllCommand.Execute(null);
        Assert.All(viewModel.Roots, x => Assert.True(x.IsExpanded));
    }

    private static (FakeApiClient Api, ProjectDto Project,
        PbsNodeDto Area, PbsNodeDto Unit, PbsNodeDto System) CreateTree()
    {
        var api = new FakeApiClient();
        var project = api.Project;
        var area = new PbsNodeDto(Guid.NewGuid(), api.Plant.Id, null, "AREA-A", "A 区", "Area", 1, null, 1, 0);
        var unit = new PbsNodeDto(Guid.NewGuid(), api.Plant.Id, area.Id, "CDU", "常减压装置", "Unit", 2, null, 1, 0);
        var system = new PbsNodeDto(Guid.NewGuid(), api.Plant.Id, unit.Id, "INS", "仪表系统", "System", 3, null, 0, 0);
        api.PbsNodes.AddRange(new[] { area, unit, system });
        return (api, project, area, unit, system);
    }

    private static PbsViewModel CreateViewModel(
        FakeApiClient api,
        ProjectDto project,
        IPbsDialogService? dialogs = null,
        IEquipmentDialogService? equipmentDialogs = null)
    {
        var session = api.CreateSession();
        var context = new ProjectContext { CurrentProject = project };
        return new PbsViewModel(
            session,
            context,
            dialogs ?? new FakePbsDialogService(),
            equipmentDialogs ?? new FakeEquipmentDialogService());
    }
}
