using WinDesign.AdminClient.ViewModels;
using WinDesign.Contracts.Permissions;
using WinDesign.Contracts.Users;

namespace WinDesign.Client.Tests;

public class UserClassViewModelTests
{
    [Fact]
    public async Task Refresh_LoadsRoleAndPermissionMatrix()
    {
        var api = new FakeApiClient();
        var adminRole = new RoleDto(Guid.NewGuid(), "Admin", "系统管理员", null);
        api.Roles.Add(adminRole);
        api.RolePermissionCodes[adminRole.Id] = PermissionCatalog.AllCodes().ToList();

        var viewModel = new UserClassViewModel(api.CreateManagementSession());
        await viewModel.RefreshAsync();

        Assert.Single(viewModel.Roles);
        Assert.Equal(adminRole.Id, viewModel.SelectedRole?.Id);
        Assert.Equal(PermissionCatalog.Modules.Count, viewModel.PermissionRows.Count);

        var equipment = viewModel.PermissionRows.Single(x => x.ModuleCode == "equipment");
        Assert.True(equipment.CanCreate);
        Assert.True(equipment.CanRead);
        Assert.True(equipment.CanUpdate);
        Assert.True(equipment.CanDelete);
    }

    [Fact]
    public async Task Save_PersistsCheckedCodes()
    {
        var api = new FakeApiClient();
        var designerRole = new RoleDto(Guid.NewGuid(), "Designer", "设计人员", null);
        api.Roles.Add(designerRole);
        api.RolePermissionCodes[designerRole.Id] = new List<string>
        {
            PermissionCatalog.Code("equipment", "read")
        };

        var viewModel = new UserClassViewModel(api.CreateManagementSession());
        await viewModel.RefreshAsync();

        var equipment = viewModel.PermissionRows.Single(x => x.ModuleCode == "equipment");
        Assert.True(equipment.CanRead);
        Assert.False(equipment.CanCreate);

        equipment.CanCreate = true;
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Contains(
            PermissionCatalog.Code("equipment", "create"),
            api.RolePermissionCodes[designerRole.Id]);
        Assert.Contains("重新登录", viewModel.StatusMessage);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task ClearAll_OnlyClearsLocalSelectionUntilSaved()
    {
        var api = new FakeApiClient();
        var viewerRole = new RoleDto(Guid.NewGuid(), "Viewer", "只读用户", null);
        api.Roles.Add(viewerRole);
        api.RolePermissionCodes[viewerRole.Id] = new List<string>
        {
            PermissionCatalog.Code("equipment", "read")
        };

        var viewModel = new UserClassViewModel(api.CreateManagementSession());
        await viewModel.RefreshAsync();

        viewModel.ClearAllCommand.Execute(null);

        Assert.All(viewModel.PermissionRows, row => Assert.False(row.CanRead));
        Assert.Contains(
            PermissionCatalog.Code("equipment", "read"),
            api.RolePermissionCodes[viewerRole.Id]);
    }
}
