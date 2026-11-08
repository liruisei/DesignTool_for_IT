using WinDesign.Client.ViewModels;

namespace WinDesign.Client.Tests;

public class ProjectManagementViewModelTests
{
    [Fact]
    public async Task SavePending_SavesEditedExistingProject()
    {
        var api = new FakeApiClient();
        var viewModel = new ProjectManagementViewModel(api.CreateSession());
        await viewModel.RefreshAsync();

        viewModel.SelectedProject = viewModel.Projects.Single();
        viewModel.EditName = "改名后的项目";

        await viewModel.SavePendingAsync();

        Assert.Equal("改名后的项目", api.Project.Name);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task SavePending_DoesNotCreateUnsavedNewProject()
    {
        var api = new FakeApiClient();
        var viewModel = new ProjectManagementViewModel(api.CreateSession());
        await viewModel.RefreshAsync();

        viewModel.NewProjectCommand.Execute(null);
        viewModel.EditCode = "NEW-001";
        viewModel.EditName = "未保存的新项目";
        var originalName = api.Project.Name;

        await viewModel.SavePendingAsync();

        Assert.Equal(originalName, api.Project.Name);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task SavePending_InvalidExistingProject_ThrowsAndKeepsData()
    {
        var api = new FakeApiClient();
        var viewModel = new ProjectManagementViewModel(api.CreateSession());
        await viewModel.RefreshAsync();

        viewModel.SelectedProject = viewModel.Projects.Single();
        viewModel.EditStartDate = new DateTime(2026, 5, 1);
        viewModel.EditEndDate = new DateTime(2026, 4, 1);

        await Assert.ThrowsAsync<WinDesign.Client.Services.ApiException>(
            () => viewModel.SavePendingAsync());

        Assert.Contains("不能早于", viewModel.ErrorMessage);
    }
}
