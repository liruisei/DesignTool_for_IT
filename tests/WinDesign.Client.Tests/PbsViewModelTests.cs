using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
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
        var (api, project, root, area, system) = CreateTree();
        var viewModel = CreateViewModel(api, project);

        await viewModel.RefreshAsync();

        Assert.Single(viewModel.Roots);
        Assert.Single(viewModel.Roots[0].Children);
        Assert.Single(viewModel.Roots[0].Children[0].Children);
        Assert.Contains(viewModel.ParentOptions, x => x.Id == system.Id);
        Assert.Contains(viewModel.ParentOptions, x => x.Id == area.Id);
        Assert.Contains(viewModel.ParentOptions, x => x.Id == root.Id);
    }

    [Fact]
    public async Task NewChild_OpensEditorWithSelectedParent_AndCreatesNode()
    {
        var (api, project, root, _, _) = CreateTree();
        var dialogs = new FakePbsDialogService
        {
            NextResult = new PbsNodeEditorResult(root.Id, "NEW-CHILD", "新子节点", "System", 5, null)
        };
        var viewModel = CreateViewModel(api, project, dialogs);
        await viewModel.RefreshAsync();
        viewModel.SelectedNode = viewModel.Roots[0];

        await viewModel.NewChildCommand.ExecuteAsync(null);

        Assert.NotNull(dialogs.LastContext);
        Assert.Equal(root.Id, dialogs.LastContext!.DefaultParentId);
        Assert.Contains(api.PbsNodes, x => x.Code == "NEW-CHILD");
    }

    [Fact]
    public async Task NewSibling_OpensEditorWithSameParent_AndCreatesNode()
    {
        var (api, project, root, _, _) = CreateTree();
        var dialogs = new FakePbsDialogService
        {
            NextResult = new PbsNodeEditorResult(root.Id, "SIBLING", "同级节点", "Area", 6, null)
        };
        var viewModel = CreateViewModel(api, project, dialogs);
        await viewModel.RefreshAsync();
        viewModel.SelectedNode = viewModel.Roots[0].Children[0];

        await viewModel.NewSiblingCommand.ExecuteAsync(null);

        Assert.Equal(root.Id, dialogs.LastContext!.DefaultParentId);
        Assert.Contains(api.PbsNodes, x => x.Code == "SIBLING");
    }

    [Fact]
    public async Task NewSibling_OnRoot_ShowsError()
    {
        var (api, project, _, _, _) = CreateTree();
        var viewModel = CreateViewModel(api, project);
        await viewModel.RefreshAsync();
        viewModel.SelectedNode = viewModel.Roots[0];

        await viewModel.NewSiblingCommand.ExecuteAsync(null);

        Assert.Contains("根节点", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task EditNode_UpdatesSelectedNode()
    {
        var (api, project, root, _, _) = CreateTree();
        var dialogs = new FakePbsDialogService
        {
            NextResult = new PbsNodeEditorResult(root.ParentId, root.Code, "改名后的节点", root.NodeType, root.SortOrder, null)
        };
        var viewModel = CreateViewModel(api, project, dialogs);
        await viewModel.RefreshAsync();
        viewModel.SelectedNode = viewModel.Roots[0];

        await viewModel.EditNodeCommand.ExecuteAsync(null);

        Assert.Equal("改名后的节点", api.PbsNodes.Single(x => x.Id == root.Id).Name);
    }

    [Fact]
    public async Task DeleteNode_ConfirmsAndDeletes()
    {
        var (api, project, _, area, _) = CreateTree();
        var dialogs = new FakePbsDialogService { ConfirmResult = true };
        var viewModel = CreateViewModel(api, project, dialogs);
        await viewModel.RefreshAsync();
        viewModel.SelectedNode = viewModel.Roots[0].Children[0];

        await viewModel.DeleteNodeCommand.ExecuteAsync(null);

        Assert.DoesNotContain(api.PbsNodes, x => x.Id == area.Id);
    }

    private static (FakeApiClient Api, ProjectDto Project,
        PbsNodeDto Root, PbsNodeDto Area, PbsNodeDto System) CreateTree()
    {
        var api = new FakeApiClient();
        var project = api.Project;
        var root = new PbsNodeDto(Guid.NewGuid(), project.Id, null, "ROOT", "全厂", "Root", 0, null, 1, 0);
        var area = new PbsNodeDto(Guid.NewGuid(), project.Id, root.Id, "AREA-A", "A 区", "Area", 1, null, 1, 0);
        var system = new PbsNodeDto(Guid.NewGuid(), project.Id, area.Id, "INS", "仪表系统", "System", 2, null, 0, 0);
        api.PbsNodes.AddRange(new[] { root, area, system });
        return (api, project, root, area, system);
    }

    private static PbsViewModel CreateViewModel(
        FakeApiClient api,
        ProjectDto project,
        IPbsDialogService? dialogs = null)
    {
        var session = new SessionService(api);
        var context = new ProjectContext { CurrentProject = project };
        return new PbsViewModel(session, context, dialogs ?? new FakePbsDialogService());
    }
}
