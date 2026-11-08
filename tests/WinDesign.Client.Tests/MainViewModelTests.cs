using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Workspaces;

namespace WinDesign.Client.Tests;

public class MainViewModelTests
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
        viewModel.OpenPbsCommand.Execute(null);
        viewModel.OpenIndexCommand.Execute(null);
        await Task.Delay(50);

        Assert.Contains(indexViewModel.Equipments, x => x.TagNo == "PT-2002");
    }

    [Fact]
    public void Explorer_HasBusinessModulesUnderProject_AndConfigurationUnderSystem()
    {
        var api = new FakeApiClient();
        var viewModel = new MainViewModel(api.CreateSession());

        var project = viewModel.ExplorerNodes.Single(x => x.Key == "project");
        var system = viewModel.ExplorerNodes.Single(x => x.Key == "system");

        Assert.Contains(project.Children, x => x.Key == "index");
        Assert.DoesNotContain(project.Children, x => x.Key == "pbs");
        Assert.DoesNotContain(project.Children, x => x.Key == "project-config");

        Assert.Contains(system.Children, x => x.Key == "pbs" && x.Title == "PBS管理");
        Assert.Contains(system.Children, x => x.Key == "project-config" && x.Title == "项目配置");
    }

    [Fact]
    public async Task SwitchWorkspace_UpdatesContextAndReopensDefaultDocument()
    {
        var api = new FakeApiClient();
        var session = api.CreateSession();
        var viewModel = new MainViewModel(session);
        viewModel.OpenPbsCommand.Execute(null);
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
        var projectConfig = new TrackingPendingViewModel();
        var dictionary = new TrackingPendingViewModel();
        viewModel.Documents.Add(new DocumentTab
        {
            Key = "project-config-test",
            Title = "项目配置",
            ViewModel = projectConfig
        });
        viewModel.Documents.Add(new DocumentTab
        {
            Key = "dictionary-test",
            Title = "编码定义",
            ViewModel = dictionary
        });

        var error = await viewModel.SavePendingDocumentsAsync();

        Assert.Null(error);
        Assert.Equal(1, projectConfig.SaveCount);
        Assert.Equal(1, dictionary.SaveCount);
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
            Title = "项目配置",
            ViewModel = failing
        });
        viewModel.Documents.Add(new DocumentTab
        {
            Key = "later-test",
            Title = "编码定义",
            ViewModel = later
        });

        var error = await viewModel.SavePendingDocumentsAsync();

        Assert.NotNull(error);
        Assert.Contains("项目配置", error);
        Assert.Contains("保存失败", error);
        Assert.Equal(0, later.SaveCount);
    }

    [Fact]
    public void ManagementMode_OnlyShowsSystem_AndOpensProjectConfig()
    {
        var api = new FakeApiClient();
        var viewModel = new MainViewModel(api.CreateManagementSession());

        Assert.DoesNotContain(viewModel.ExplorerNodes, x => x.Key == "project");
        var system = viewModel.ExplorerNodes.Single(x => x.Key == "system");
        Assert.Contains(system.Children, x => x.Key == "project-config");
        Assert.Contains(system.Children, x => x.Key == "pbs");
        Assert.Contains(viewModel.Documents, x => x.Key == "project-config");
        Assert.Equal("管理模块", viewModel.WorkspaceText);
    }

    [Fact]
    public void ApplyWorkspaceChange_FromManagementToWorkspace_RebuildsProjectNav()
    {
        var api = new FakeApiClient();
        var session = api.CreateManagementSession();
        var viewModel = new MainViewModel(session);

        var projectRef = new WorkspaceProjectDto(
            api.Project.Id, api.Project.Code, api.Project.Name, api.Project.Phase, api.Project.Status);
        session.SetWorkspace(new WorkspaceResponse(
            "fake-workspace-token",
            DateTime.UtcNow.AddHours(1),
            session.User!,
            api.Plant with { Projects = new[] { projectRef } },
            projectRef));

        viewModel.ApplyWorkspaceChange();

        Assert.Contains(viewModel.ExplorerNodes, x => x.Key == "project");
        Assert.Equal(api.Project.Name, viewModel.ProjectName);
        Assert.Contains(viewModel.Documents, x => x.Key == "index");
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
        Status = "Planned",
        Quantity = 1,
        CreatedAt = DateTime.UtcNow
    };
}
