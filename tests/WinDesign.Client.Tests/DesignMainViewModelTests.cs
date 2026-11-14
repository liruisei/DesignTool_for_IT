using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Workspaces;
using WinDesign.DesignClient.ViewModels;

namespace WinDesign.Client.Tests;

/// <summary>
/// 设计客户端外壳：只包含索引表 / 规格书 / 材料表 / 报表，不出现系统配置。
/// </summary>
public class DesignMainViewModelTests
{
    [Fact]
    public async Task ReactivatingIndexDocument_RefreshesEquipment()
    {
        var api = new FakeApiClient();
        api.Equipments.Add(CreateEquipment(api, "PT-1001"));
        var viewModel = new MainViewModel(api.CreateSession());

        var indexTab = viewModel.Documents.Single(x => x.Key == "index");
        var indexViewModel = (EquipmentIndexViewModel)indexTab.ViewModel;
        await indexViewModel.RefreshAsync();
        Assert.Single(indexViewModel.Equipments);

        api.Equipments.Add(CreateEquipment(api, "PT-2002"));
        viewModel.OpenSpecsCommand.Execute(null);
        viewModel.OpenIndexCommand.Execute(null);
        await Task.Delay(50);

        Assert.Contains(indexViewModel.Equipments, x => x.TagNo == "PT-2002");
    }

    [Fact]
    public void Explorer_HasProjectAndPbsGroups_WithDesignModules()
    {
        var api = new FakeApiClient();
        var viewModel = new MainViewModel(api.CreateSession());

        var project = Assert.Single(viewModel.ProjectExplorerNodes, x => x.Key == "project");
        Assert.Contains(project.Children, x => x.Key == "index");
        Assert.Contains(project.Children, x => x.Key == "specs");
        Assert.Contains(project.Children, x => x.Key == "materials");
        Assert.Contains(project.Children, x => x.Key == "reports");

        // PBS 独立到下方面板，两个 Explorer 互不混入对方的根节点。
        var pbs = Assert.Single(viewModel.PbsExplorerNodes, x => x.Key == "pbs");
        // 工厂根节点只显示工厂编码。
        Assert.Equal(api.Plant.Code, pbs.Title);
        Assert.DoesNotContain(pbs.Children, x => x.Key == "index");
        Assert.DoesNotContain(viewModel.ProjectExplorerNodes, x => x.Key == "pbs");
        Assert.DoesNotContain(viewModel.PbsExplorerNodes, x => x.Key == "project");

        Assert.DoesNotContain(viewModel.ProjectExplorerNodes, x => x.Key == "system");
        Assert.DoesNotContain(project.Children, x => x.Key == "pbs");
        Assert.DoesNotContain(project.Children, x => x.Key == "project-config");
        Assert.DoesNotContain(project.Children, x => x.Key == "users");
    }

    [Fact]
    public async Task PbsTree_ShowsOnlyCurrentProjectRootSubtree()
    {
        var api = new FakeApiClient();
        api.AddDefaultPbsRoot();
        var otherRoot = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, null, "AREA-B", "B 区", "Area", 2, null, 0, 0);
        var unit = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, api.PbsRoot.Id, "CDU", "常减压装置", "Unit", 1, null, 0, 0);
        var loop = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, unit.Id, "L-1001", "压力控制回路", "Loop", 1, null, 0, 0);
        api.PbsNodes.AddRange(new[] { otherRoot, unit, loop });

        var viewModel = new MainViewModel(api.CreateSession());
        await viewModel.PbsTreeLoadTask;

        var pbs = Assert.Single(viewModel.PbsExplorerNodes, x => x.Key == "pbs");
        var rootItem = Assert.Single(pbs.Children);
        Assert.Contains("AREA-A", rootItem.Title);

        var unitItem = Assert.Single(rootItem.Children);
        // 装置只显示编号，不拼接名称。
        Assert.Equal("CDU", unitItem.Title);
        Assert.Equal(new[] { "设备", "回路", "建筑物" },
            unitItem.Children.Take(3).Select(x => x.Title));

        // 回路 PBS 节点归入「回路」分类文件夹，不再作为装置的普通子节点显示。
        var loopsFolder = Assert.Single(unitItem.Children, x => x.CategoryFolder == ExplorerCategoryFolder.Loop);
        Assert.Contains(loopsFolder.Children, x => x.Title.Contains("L-1001"));
        Assert.DoesNotContain(unitItem.Children, x => x.Title.Contains("L-1001"));

        // 同一工厂下与项目无关的另一棵顶层子树不应出现。
        Assert.DoesNotContain(pbs.Children, x => x.Title.Contains("AREA-B"));
    }

    [Fact]
    public async Task UnitNode_HasDefaultCategoryFolders()
    {
        var api = new FakeApiClient();
        api.AddDefaultPbsRoot();
        var unit = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, api.PbsRoot.Id, "CDU", "常减压装置", "Unit", 1, null, 0, 0);
        api.PbsNodes.Add(unit);

        var viewModel = new MainViewModel(api.CreateSession());
        await viewModel.PbsTreeLoadTask;

        var pbs = Assert.Single(viewModel.PbsExplorerNodes, x => x.Key == "pbs");
        var rootItem = Assert.Single(pbs.Children);
        var unitItem = Assert.Single(rootItem.Children);

        Assert.Equal(new[] { "设备", "回路", "建筑物" }, unitItem.Children.Select(x => x.Title));
        Assert.All(unitItem.Children, x => Assert.True(x.IsFolder));
    }

    [Fact]
    public async Task UnitDevicesFolder_ShowsEquipmentByTagNo()
    {
        var api = new FakeApiClient();
        api.AddDefaultPbsRoot();
        var unit = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, api.PbsRoot.Id, "CDU", "常减压装置", "Unit", 1, null, 0, 0);
        api.PbsNodes.Add(unit);
        api.Equipments.Add(CreateEquipment(api, "PT-1001") with { PbsNodeId = unit.Id });

        var viewModel = new MainViewModel(api.CreateSession(), new FakeEquipmentDialogService());
        await viewModel.PbsTreeLoadTask;

        var pbs = Assert.Single(viewModel.PbsExplorerNodes, x => x.Key == "pbs");
        var unitItem = Assert.Single(Assert.Single(pbs.Children).Children);
        var devices = Assert.Single(unitItem.Children, x => x.IsEquipmentFolder);

        Assert.Equal("设备", devices.Title);
        var item = Assert.Single(devices.Children);
        Assert.Equal("PT-1001", item.Title);
        Assert.False(item.IsFolder);
    }

    [Fact]
    public async Task AddEquipmentFromDevicesFolder_CreatesAndRefreshesTree()
    {
        var api = new FakeApiClient();
        api.AddDefaultPbsRoot();
        var unit = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, api.PbsRoot.Id, "CDU", "常减压装置", "Unit", 1, null, 0, 0);
        api.PbsNodes.Add(unit);

        var dialogs = new FakeEquipmentDialogService
        {
            NextResult = new EquipmentUpsertRequest
            {
                PbsNodeId = unit.Id,
                TagNo = "PT-9001",
                Name = "压力变送器",
                Discipline = "Instrument",
                Quantity = 1
            }
        };
        var viewModel = new MainViewModel(api.CreateSession(), dialogs);
        await viewModel.PbsTreeLoadTask;

        var pbs = Assert.Single(viewModel.PbsExplorerNodes, x => x.Key == "pbs");
        var unitItem = Assert.Single(Assert.Single(pbs.Children).Children);
        viewModel.SelectedPbsNode = Assert.Single(unitItem.Children, x => x.IsEquipmentFolder);
        Assert.True(viewModel.IsEquipmentFolderSelected);

        await viewModel.AddEquipmentCommand.ExecuteAsync(null);

        Assert.NotNull(dialogs.LastContext);
        Assert.Equal(unit.Id, dialogs.LastContext!.DefaultPbsNodeId);
        Assert.Contains(api.Equipments, x => x.TagNo == "PT-9001");

        var refreshedUnit = Assert.Single(Assert.Single(
            Assert.Single(viewModel.PbsExplorerNodes, x => x.Key == "pbs").Children).Children);
        var refreshedDevices = Assert.Single(refreshedUnit.Children, x => x.IsEquipmentFolder);
        Assert.Contains(refreshedDevices.Children, x => x.Title == "PT-9001");
    }

    [Fact]
    public async Task PbsTree_WithoutProjectRoot_ShowsAllFactoryTopLevelNodes()
    {
        var api = new FakeApiClient();
        api.AddDefaultPbsRoot();
        api.Project = api.Project with { PbsRootNodeId = null };
        var otherRoot = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, null, "AREA-B", "B 区", "Area", 2, null, 0, 0);
        api.PbsNodes.Add(otherRoot);

        var viewModel = new MainViewModel(api.CreateSession());
        await viewModel.PbsTreeLoadTask;

        var pbs = Assert.Single(viewModel.PbsExplorerNodes, x => x.Key == "pbs");
        Assert.Equal(2, pbs.Children.Count);
        Assert.Contains(pbs.Children, x => x.Title.Contains("AREA-A"));
        Assert.Contains(pbs.Children, x => x.Title.Contains("AREA-B"));
    }

    [Fact]
    public async Task SwitchWorkspace_UpdatesContextAndReopensDefaultDocument()
    {
        var api = new FakeApiClient();
        var session = api.CreateSession();
        var viewModel = new MainViewModel(session);
        viewModel.OpenSpecsCommand.Execute(null);
        Assert.Equal(2, viewModel.Documents.Count);
        var oldIndexViewModel = viewModel.Documents.Single(x => x.Key == "index").ViewModel;

        var projectB = new WorkspaceProjectDto(
            Guid.NewGuid(), "DEMO-002", "常减压扩能项目", "基础设计", "Active");
        session.SetWorkspace(new WorkspaceResponse(
            "fake-workspace-token-2",
            DateTime.UtcNow.AddHours(1),
            session.User!,
            session.Plant!,
            projectB));

        viewModel.ApplyWorkspaceChange();

        Assert.Equal("常减压扩能项目", viewModel.ProjectName);
        Assert.Contains("常减压扩能项目", viewModel.WorkspaceText);
        Assert.Single(viewModel.Documents);
        var indexTab = viewModel.Documents.Single(x => x.Key == "index");
        Assert.NotSame(oldIndexViewModel, indexTab.ViewModel);
        Assert.Equal(projectB.Id,
            ((EquipmentIndexViewModel)indexTab.ViewModel).Context.CurrentProject!.Id);
    }

    [Fact]
    public async Task SavePendingDocuments_SavesEveryOpenPendingDocument()
    {
        var api = new FakeApiClient();
        var viewModel = new MainViewModel(api.CreateSession());
        var first = new TrackingPendingViewModel();
        var second = new TrackingPendingViewModel();
        viewModel.Documents.Add(new DocumentTab
        {
            Key = "first-test",
            Title = "规格书",
            ViewModel = first
        });
        viewModel.Documents.Add(new DocumentTab
        {
            Key = "second-test",
            Title = "材料表",
            ViewModel = second
        });

        var error = await viewModel.SavePendingDocumentsAsync();

        Assert.Null(error);
        Assert.Equal(1, first.SaveCount);
        Assert.Equal(1, second.SaveCount);
    }

    [Fact]
    public async Task SavePendingDocuments_ReturnsFirstError_AndStopsSaving()
    {
        var api = new FakeApiClient();
        var viewModel = new MainViewModel(api.CreateSession());
        var failing = new TrackingPendingViewModel { Error = new ApiException("保存失败") };
        var later = new TrackingPendingViewModel();
        viewModel.Documents.Add(new DocumentTab
        {
            Key = "failing-test",
            Title = "索引表",
            ViewModel = failing
        });
        viewModel.Documents.Add(new DocumentTab
        {
            Key = "later-test",
            Title = "规格书",
            ViewModel = later
        });

        var error = await viewModel.SavePendingDocumentsAsync();

        Assert.NotNull(error);
        Assert.Contains("索引表", error);
        Assert.Contains("保存失败", error);
        Assert.Equal(0, later.SaveCount);
    }

    [Theory]
    [InlineData(ExplorerCategoryFolder.Loop, "Loop", "回路", "L-9001")]
    [InlineData(ExplorerCategoryFolder.Building, "Building", "建筑物", "BLD-01")]
    public async Task AddFromCategoryFolder_CreatesTypedPbsChild_AndRefreshesTree(
        ExplorerCategoryFolder category,
        string nodeType,
        string folderTitle,
        string code)
    {
        var api = new FakeApiClient();
        api.AddDefaultPbsRoot();
        var unit = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, api.PbsRoot.Id, "CDU", "常减压装置", "Unit", 1, null, 0, 0);
        api.PbsNodes.Add(unit);

        var dialogs = new FakeCategoryDialogService
        {
            NextResult = new CategoryEditResult(code, code + " 名称", "备注")
        };
        var viewModel = new MainViewModel(
            api.CreateSession(),
            new FakeEquipmentDialogService(),
            dialogs);
        await viewModel.PbsTreeLoadTask;

        var pbs = Assert.Single(viewModel.PbsExplorerNodes, x => x.Key == "pbs");
        var unitItem = Assert.Single(Assert.Single(pbs.Children).Children);
        viewModel.SelectedPbsNode = Assert.Single(unitItem.Children, x => x.CategoryFolder == category);

        Assert.True(viewModel.IsCategoryFolderSelected);
        Assert.False(viewModel.IsEquipmentFolderSelected);
        Assert.Equal(folderTitle, viewModel.SelectedPbsNode!.Title);

        await viewModel.AddCategoryItemCommand.ExecuteAsync(null);

        Assert.NotNull(dialogs.LastContext);
        Assert.Equal(category, dialogs.LastContext!.Category);
        var call = Assert.Single(api.UnitChildren);
        Assert.Equal(unit.Id, call.UnitId);
        Assert.Equal(nodeType, call.Request.NodeType);
        Assert.Equal(code, call.Request.Code);

        var refreshedUnit = Assert.Single(Assert.Single(
            Assert.Single(viewModel.PbsExplorerNodes, x => x.Key == "pbs").Children).Children);
        var refreshedFolder = Assert.Single(refreshedUnit.Children, x => x.CategoryFolder == category);
        Assert.Contains(refreshedFolder.Children, x => x.Title.Contains(code));
    }

    [Fact]
    public async Task AddFromCategoryFolder_Cancelled_DoesNotCreate()
    {
        var api = new FakeApiClient();
        api.AddDefaultPbsRoot();
        var unit = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, api.PbsRoot.Id, "CDU", "常减压装置", "Unit", 1, null, 0, 0);
        api.PbsNodes.Add(unit);

        var viewModel = new MainViewModel(
            api.CreateSession(),
            new FakeEquipmentDialogService(),
            new FakeCategoryDialogService { NextResult = null });
        await viewModel.PbsTreeLoadTask;

        var pbs = Assert.Single(viewModel.PbsExplorerNodes, x => x.Key == "pbs");
        var unitItem = Assert.Single(Assert.Single(pbs.Children).Children);
        viewModel.SelectedPbsNode = Assert.Single(
            unitItem.Children, x => x.CategoryFolder == ExplorerCategoryFolder.Loop);

        await viewModel.AddCategoryItemCommand.ExecuteAsync(null);

        Assert.Empty(api.UnitChildren);
        Assert.DoesNotContain(api.PbsNodes, x => x.ParentId == unit.Id);
    }

    [Fact]
    public async Task EditBuildingNode_UpdatesCodeAndName_AndRefreshesTree()
    {
        var api = new FakeApiClient();
        api.AddDefaultPbsRoot();
        var unit = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, api.PbsRoot.Id, "1885", "全厂信息管理系统", "Unit", 1, null, 0, 0);
        var building = new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, unit.Id, "FAR3-01", "煤制氢联合装置机柜间", "Building", 1, null, 0, 0);
        api.PbsNodes.AddRange(new[] { unit, building });

        var dialogs = new FakeCategoryDialogService
        {
            NextResult = new CategoryEditResult("FAR3-01-E", "机柜间-改", "已编辑")
        };
        var viewModel = new MainViewModel(
            api.CreateSession(),
            new FakeEquipmentDialogService(),
            dialogs);
        await viewModel.PbsTreeLoadTask;

        var pbs = Assert.Single(viewModel.PbsExplorerNodes, x => x.Key == "pbs");
        var unitItem = Assert.Single(Assert.Single(pbs.Children).Children);
        var buildingsFolder = Assert.Single(unitItem.Children,
            x => x.CategoryFolder == ExplorerCategoryFolder.Building);
        viewModel.SelectedPbsNode = Assert.Single(buildingsFolder.Children);

        Assert.True(viewModel.IsBuildingNodeSelected);
        Assert.True(viewModel.ShowPbsContextActions);

        await viewModel.EditPbsBuildingCommand.ExecuteAsync(null);

        Assert.NotNull(dialogs.LastContext);
        Assert.True(dialogs.LastContext!.IsEdit);
        Assert.Equal("FAR3-01", dialogs.LastContext.Code);

        var update = Assert.Single(api.UnitChildUpdates);
        Assert.Equal(unit.Id, update.UnitId);
        Assert.Equal(building.Id, update.Id);
        Assert.Equal("FAR3-01-E", update.Request.Code);

        var refreshedUnit = Assert.Single(Assert.Single(
            Assert.Single(viewModel.PbsExplorerNodes, x => x.Key == "pbs").Children).Children);
        var refreshedFolder = Assert.Single(refreshedUnit.Children,
            x => x.CategoryFolder == ExplorerCategoryFolder.Building);
        Assert.Contains(refreshedFolder.Children, x => x.Title == "FAR3-01-E");
    }

    private sealed class FakeCategoryDialogService : ICategoryDialogService
    {
        public CategoryEditorContext? LastContext { get; private set; }
        public CategoryEditResult? NextResult { get; set; }

        public CategoryEditResult? EditCategory(CategoryEditorContext context)
        {
            LastContext = context;
            return NextResult;
        }
    }

    private sealed class TrackingPendingViewModel : ViewModelBase, IPendingChanges
    {
        public int SaveCount { get; private set; }
        public Exception? Error { get; set; }

        public Task SavePendingAsync()
        {
            SaveCount++;
            return Error is null ? Task.CompletedTask : Task.FromException(Error);
        }
    }

    private static EquipmentDto CreateEquipment(FakeApiClient api, string tagNo) => new()
    {
        Id = Guid.NewGuid(),
        PlantId = api.Plant.Id,
        ProjectId = api.Project.Id,
        TagNo = tagNo,
        Name = "压力变送器",
        Discipline = "Instrument",
        Quantity = 1,
        CreatedAt = DateTime.UtcNow
    };
}
