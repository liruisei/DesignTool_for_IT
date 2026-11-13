using WinDesign.AdminClient.ViewModels;
using WinDesign.Contracts.Projects;
using WinDesign.Contracts.Users;

namespace WinDesign.Client.Tests;

public class ProjectManagementViewModelTests
{
    [Fact]
    public async Task SavePending_SavesEditedExistingProject()
    {
        var api = new FakeApiClient();
        api.AddDefaultPbsRoot();
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
        api.AddDefaultPbsRoot();
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
    public async Task ManagementMode_LoadsAllProjectsAndPlants()
    {
        var api = new FakeApiClient();
        api.AddDefaultPbsRoot();
        var viewModel = new ProjectManagementViewModel(api.CreateManagementSession());

        await viewModel.RefreshAsync();

        Assert.True(viewModel.IsManagementMode);
        Assert.Single(viewModel.Plants);
        Assert.True(viewModel.CanSelectPlant);
        Assert.Single(viewModel.Projects);
    }

    [Fact]
    public async Task SavePending_InvalidExistingProject_ThrowsAndKeepsData()
    {
        var api = new FakeApiClient();
        api.AddDefaultPbsRoot();
        var viewModel = new ProjectManagementViewModel(api.CreateSession());
        await viewModel.RefreshAsync();

        viewModel.SelectedProject = viewModel.Projects.Single();
        viewModel.EditStartDate = new DateTime(2026, 5, 1);
        viewModel.EditEndDate = new DateTime(2026, 4, 1);

        await Assert.ThrowsAsync<WinDesign.Client.Services.ApiException>(
            () => viewModel.SavePendingAsync());

        Assert.Contains("不能早于", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task SelectingProject_LoadsMembersAndUserSelectionList()
    {
        var api = new FakeApiClient();
        api.AddDefaultPbsRoot();
        var memberUser = NewUser("designer", "设计人员");
        var candidate = NewUser("viewer", "只读用户");
        api.Users.AddRange(new[] { memberUser, candidate });
        api.ProjectMembers.Add(new ProjectMemberDto(
            memberUser.Id, memberUser.UserName, memberUser.DisplayName, "Designer", true));

        var viewModel = new ProjectManagementViewModel(api.CreateSession());
        await viewModel.RefreshAsync();

        viewModel.SelectedProject = viewModel.Projects.Single();

        Assert.Single(viewModel.Members);
        Assert.Equal(memberUser.Id, viewModel.Members[0].UserId);
        Assert.Contains(viewModel.UserSelectionItems, x => x.UserId == candidate.Id);
        Assert.Contains(viewModel.UserSelectionItems, x => x.UserId == memberUser.Id);
    }

    [Fact]
    public async Task GrantMembers_GrantsCheckedUsersWithSelectedRole()
    {
        var api = new FakeApiClient();
        api.AddDefaultPbsRoot();
        var candidate = NewUser("viewer", "只读用户");
        api.Users.Add(candidate);

        var viewModel = new ProjectManagementViewModel(api.CreateSession());
        await viewModel.RefreshAsync();
        viewModel.SelectedProject = viewModel.Projects.Single();

        var item = viewModel.UserSelectionItems.Single(x => x.UserId == candidate.Id);
        item.IsSelected = true;
        viewModel.GrantRole = "Viewer";
        await viewModel.GrantMembersCommand.ExecuteAsync(null);

        Assert.Contains(api.ProjectMembers,
            x => x.UserId == candidate.Id && x.RoleInProject == "Viewer");
        Assert.Contains(viewModel.Members, x => x.UserId == candidate.Id);
        Assert.False(item.IsSelected);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task UpdateMemberRole_ChangesRoleOnServer()
    {
        var api = new FakeApiClient();
        api.AddDefaultPbsRoot();
        var memberUser = NewUser("designer", "设计人员");
        api.Users.Add(memberUser);
        api.ProjectMembers.Add(new ProjectMemberDto(
            memberUser.Id, memberUser.UserName, memberUser.DisplayName, "Designer", true));

        var viewModel = new ProjectManagementViewModel(api.CreateSession());
        await viewModel.RefreshAsync();
        viewModel.SelectedProject = viewModel.Projects.Single();
        viewModel.SelectedMember = viewModel.Members.Single();

        viewModel.EditMemberRole = "Viewer";
        await viewModel.UpdateMemberRoleCommand.ExecuteAsync(null);

        Assert.Equal("Viewer", api.ProjectMembers.Single().RoleInProject);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task RemoveMember_RemovesUserFromProject()
    {
        var api = new FakeApiClient();
        api.AddDefaultPbsRoot();
        var memberUser = NewUser("designer", "设计人员");
        api.Users.Add(memberUser);
        api.ProjectMembers.Add(new ProjectMemberDto(
            memberUser.Id, memberUser.UserName, memberUser.DisplayName, "Designer", true));

        var viewModel = new ProjectManagementViewModel(api.CreateSession());
        await viewModel.RefreshAsync();
        viewModel.SelectedProject = viewModel.Projects.Single();
        viewModel.SelectedMember = viewModel.Members.Single();

        await viewModel.RemoveMemberCommand.ExecuteAsync(null);

        Assert.Empty(api.ProjectMembers);
        Assert.Empty(viewModel.Members);
        Assert.Contains(viewModel.UserSelectionItems, x => x.UserId == memberUser.Id);
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
        Array.Empty<string>(),
        DateTime.UtcNow,
        null);
}
