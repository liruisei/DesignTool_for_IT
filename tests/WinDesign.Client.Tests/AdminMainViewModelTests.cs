using WinDesign.AdminClient.ViewModels;
using WinDesign.Client.Services;

namespace WinDesign.Client.Tests;

/// <summary>
/// 管理客户端外壳：只包含系统配置，默认打开 PBS 管理，不绑定工厂/项目。
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

        // 导航顺序：PBS管理 → 项目配置 → 编码定义 → 编号规定 → 用户管理 → 设备样本库 → 权限管理
        Assert.Equal(
            new[] { "pbs", "project-config", "dictionaries", "numbering", "users", "switches", "user-classes" },
            system.Children.Select(x => x.Key));
        Assert.Equal("编码定义", system.Children.Single(x => x.Key == "dictionaries").Title);
        Assert.Equal("编号规定", system.Children.Single(x => x.Key == "numbering").Title);

        Assert.DoesNotContain(viewModel.ExplorerNodes, x => x.Key == "project");
        Assert.DoesNotContain(system.Children, x => x.Key == "index");
    }

    [Fact]
    public void Constructor_OpensPbs_AsDefaultDocument()
    {
        var api = new FakeApiClient();
        var viewModel = new MainViewModel(api.CreateManagementSession());

        var pbs = Assert.Single(viewModel.Documents, x => x.Key == "pbs" && x.Title == "PBS管理");
        Assert.False(pbs.IsClosable);
        Assert.DoesNotContain(viewModel.Documents, x => x.Key == "project-config");
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
