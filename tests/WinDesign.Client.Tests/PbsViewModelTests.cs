using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Projects;

namespace WinDesign.Client.Tests;

public sealed class FakePbsDialogService : IPbsDialogService
{
    public PbsNodeEditorContext? LastContext { get; private set; }
    public PbsNodeEditorResult? NextResult { get; set; }
    public bool ConfirmResult { get; set; } = true;

    public PbsNodeEditorResult? EditPbsNode(PbsNodeEditorContext context)
    {
        LastContext = context;
        return NextResult;
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
        Assert.Single(viewModel.Roots[0].Children);
        Assert.Single(viewModel.Roots[0].Children[0].Children);
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
        viewModel.SelectedNode = viewModel.Roots[0];

        await viewModel.NewChildCommand.ExecuteAsync(null);

        Assert.NotNull(dialogs.LastContext);
        Assert.Equal(area.Id, dialogs.LastContext!.DefaultParentId);
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
        viewModel.SelectedNode = viewModel.Roots[0].Children[0];

        await viewModel.NewSiblingCommand.ExecuteAsync(null);

        Assert.Equal(area.Id, dialogs.LastContext!.DefaultParentId);
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
        viewModel.SelectedNode = viewModel.Roots[0];

        await viewModel.NewSiblingCommand.ExecuteAsync(null);

        Assert.Null(dialogs.LastContext!.DefaultParentId);
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
        viewModel.SelectedNode = viewModel.Roots[0];

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
        viewModel.SelectedNode = viewModel.Roots[0].Children[0];

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
            Status = "Planned",
            Quantity = 1,
            CreatedAt = DateTime.UtcNow
        });
        var viewModel = CreateViewModel(api, project);

        await viewModel.RefreshAsync();

        var systemItem = viewModel.Roots[0].Children[0].Children[0];
        Assert.Contains(systemItem.Children, x => x.Equipment?.TagNo == "PT-9001");
    }

    [Fact]
    public async Task AddEquipment_UnderLoop_PrefillsPbsAndLoopNo()
    {
        var (api, project, _, _, system) = CreateTree();
        var loop = new PbsNodeDto(Guid.NewGuid(), api.Plant.Id, system.Id, "L-1001", "压力回路", "Loop", 0, null, 0, 0);
        api.PbsNodes.Add(loop);
        var equipmentDialogs = new FakeEquipmentDialogService
        {
            NextResult = new EquipmentUpsertRequest
            {
                PbsNodeId = loop.Id,
                TagNo = "PT-9101",
                Name = "设备",
                Discipline = "Instrument",
                Status = "Planned",
                Quantity = 1
            }
        };
        var viewModel = CreateViewModel(api, project, equipmentDialogs: equipmentDialogs);
        await viewModel.RefreshAsync();
        viewModel.SelectedNode = viewModel.Roots[0].Children[0].Children[0].Children[0];

        await viewModel.AddEquipmentCommand.ExecuteAsync(null);

        Assert.NotNull(equipmentDialogs.LastContext);
        Assert.Equal(loop.Id, equipmentDialogs.LastContext!.DefaultPbsNodeId);
        Assert.Equal("L-1001", equipmentDialogs.LastContext.DefaultLoopNo);
        Assert.Contains(api.Equipments, x => x.TagNo == "PT-9101");
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
