using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Auth;

namespace WinDesign.Client.Tests;

public class WorkspaceSelectionViewModelTests
{
    [Fact]
    public async Task Load_PreselectsCurrentWorkspace_WhenSwitching()
    {
        var api = new FakeApiClient();
        var session = api.CreateSession();

        var viewModel = new WorkspaceSelectionViewModel(
            session, session.PlantId, session.ProjectId);
        await WaitUntilLoadedAsync(viewModel);

        Assert.True(viewModel.IsSwitching);
        Assert.Equal("切换工作区", viewModel.WindowTitle);
        Assert.Equal(session.PlantId, viewModel.SelectedPlant?.Id);
        Assert.Equal(session.ProjectId, viewModel.SelectedProject?.Id);
    }

    [Fact]
    public async Task Confirm_SignsScopedWorkspaceTokenIntoSession()
    {
        var api = new FakeApiClient();
        var session = new SessionService(api);
        session.SignIn(new LoginResponse(
            "identity-token",
            DateTime.UtcNow.AddHours(1),
            new CurrentUserDto(Guid.NewGuid(), "admin", "管理员",
                new[] { "Admin" }, Array.Empty<string>())));

        var viewModel = new WorkspaceSelectionViewModel(session);
        await WaitUntilLoadedAsync(viewModel);
        Assert.False(session.HasWorkspace);

        var raised = false;
        viewModel.SelectionSucceeded += (_, _) => raised = true;

        await viewModel.ConfirmCommand.ExecuteAsync(null);

        Assert.True(raised);
        Assert.True(session.HasWorkspace);
        Assert.Equal(api.Plant.Id, session.PlantId);
        Assert.Equal(api.Project.Id, session.ProjectId);
        Assert.Equal("fake-workspace-token", api.Token);
    }

    private static async Task WaitUntilLoadedAsync(WorkspaceSelectionViewModel viewModel)
    {
        for (var i = 0; i < 100 && viewModel.IsLoading; i++)
        {
            await Task.Delay(10);
        }
    }
}
