using System.Windows;
using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Client.Views;
using WinDesign.DesignClient.ViewModels;
using WinDesign.DesignClient.Views;

namespace WinDesign.DesignClient;

/// <summary>
/// 设计客户端：登录后必须先选择「工厂 → 项目」，再进入索引表 / 规格书 / 材料表 / 报表。
/// 系统配置类功能不在本客户端出现，由管理客户端负责。
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var session = new SessionService();
        var settings = SettingsService.Load();

        while (true)
        {
            var loginViewModel = new LoginViewModel(session, settings, "设计模块");
            var loginWindow = new LoginWindow { DataContext = loginViewModel };
            if (loginWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            settings = new AppSettings(session.Api.BaseUrl, session.User?.UserName ?? settings.UserName);
            SettingsService.Save(settings);

            // 登录成功后必须先选择工作区：工厂（PBS 根节点）→ 项目。
            var workspaceSelected = false;
            while (!workspaceSelected)
            {
                var workspaceViewModel = new WorkspaceSelectionViewModel(session);
                var workspaceWindow = new WorkspaceSelectionWindow { DataContext = workspaceViewModel };
                if (workspaceWindow.ShowDialog() != true)
                {
                    session.SignOut();
                    break;
                }

                workspaceSelected = true;
            }

            if (!workspaceSelected)
            {
                continue;
            }

            var mainViewModel = new MainViewModel(session);
            var mainWindow = new MainWindow { DataContext = mainViewModel };
            var logout = false;
            mainViewModel.LogoutRequested += (_, _) =>
            {
                logout = true;
                mainWindow.Close();
            };
            mainWindow.ShowDialog();

            if (!logout)
            {
                Shutdown();
                return;
            }

            settings = new AppSettings(session.Api.BaseUrl, session.User?.UserName ?? settings.UserName);
        }
    }
}
