using WinDesign.AdminClient.ViewModels;
using WinDesign.Contracts.Users;

namespace WinDesign.Client.Tests;

public class UserManagementViewModelTests
{
    [Fact]
    public async Task DeleteUser_RemovesUserAndResetsEditor()
    {
        var api = new FakeApiClient();
        var user = NewUser("temp", "临时用户");
        api.Users.Add(user);

        var viewModel = new UserManagementViewModel(api.CreateManagementSession());
        await viewModel.RefreshAsync();
        viewModel.SelectedUser = viewModel.Users.Single();

        await viewModel.DeleteUserCommand.ExecuteAsync(null);

        Assert.Empty(api.Users);
        Assert.Empty(viewModel.Users);
        Assert.Null(viewModel.SelectedUser);
        Assert.True(viewModel.IsCreating);
        Assert.Contains("已删除用户", viewModel.StatusMessage);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task SaveActiveChanges_CommitsPendingToggles()
    {
        var api = new FakeApiClient();
        var user = NewUser("toggle", "启停测试");
        api.Users.Add(user);

        var viewModel = new UserManagementViewModel(api.CreateManagementSession());
        await viewModel.RefreshAsync();

        var row = viewModel.UserRows.Single();
        row.IsActive = false;
        Assert.True(viewModel.HasPendingActiveChanges);
        Assert.True(api.Users.Single().IsActive); // 勾选只改本地，未提交

        await viewModel.SaveActiveChangesCommand.ExecuteAsync(null);

        Assert.False(api.Users.Single().IsActive);
        Assert.False(viewModel.HasPendingActiveChanges);
        Assert.Contains("已保存", viewModel.StatusMessage);
        Assert.Null(viewModel.ErrorMessage);
    }

    private static UserDto NewUser(string userName, string displayName) => new(
        Guid.NewGuid(),
        userName,
        displayName,
        null,
        null,
        true,
        false,
        new[] { "Viewer" },
        DateTime.UtcNow,
        null);
}
