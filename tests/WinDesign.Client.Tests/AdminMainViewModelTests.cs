using WinDesign.AdminClient.ViewModels;
using WinDesign.Client.Services;

namespace WinDesign.Client.Tests;

/// <summary>
/// 管理客户端外壳：只包含系统配置，默认打开项目配置，不绑定工厂/项目。
/// </summary>
public class AdminMainViewModelTests
{
    [Fact]
    public void Explorer_OnlyShowsSystemConfiguration()
    {
        var api = new FakeApiClient();
        var viewModel = new MainViewModel(api.CreateManagementSession());

        var system = Assert.Single(viewModel.ExplorerNodes);
        Assert.Equal("system", system.Key);
        Assert.Contains(system.Children, x => x.Key == "project-config");
        Assert.Contains(system.Children, x => x.Key == "pbs");
        Assert.Contains(system.Children, x => x.Key == "dictionaries");
        Assert.Contains(system.Children, x => x.Key == "users");

        Assert.DoesNotContain(viewModel.ExplorerNodes, x => x.Key == "project");
        Assert.DoesNotContain(system.Children, x => x.Key == "index");
    }

    [Fact]
    public void Constructor_OpensProjectConfig_AsDefaultDocument()
    {
        var api = new FakeApiClient();
        var viewModel = new MainViewModel(api.CreateManagementSession());

        Assert.Contains(viewModel.Documents, x => x.Key == "project-config");
        Assert.Equal("系统配置（不绑定工厂/项目）", viewModel.WorkspaceText);
    }

    [Fact]
    public void OpenPbs_AddsPbsDocument()
    {
        var api = new FakeApiClient();
        var viewModel = new MainViewModel(api.CreateManagementSession());

        viewModel.OpenPbsCommand.Execute(null);

        Assert.Contains(viewModel.Documents, x => x.Key == "pbs" && x.Title == "PBS管理");
    }
}
