using System.Windows;
using WinDesign.AdminClient.ViewModels;
using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Client.Views;

namespace WinDesign.AdminClient;

/// <summary>
/// 管理客户端：只负责系统配置，不绑定工厂/项目。
/// 登录后必须通过 /api/auth/management 换取“管理客户端令牌”，非管理员直接拒绝。
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var session = new SessionService();
        var settings = SettingsService.Load("admin");

        while (true)
        {
            var loginViewModel = new LoginViewModel(session, settings, "管理模块");
            var loginWindow = new LoginWindow { DataContext = loginViewModel };
            if (loginWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            settings = new AppSettings(session.Api.BaseUrl, session.User?.UserName ?? settings.UserName);
            SettingsService.Save("admin", settings);

            if (!session.IsAdmin)
            {
                MessageBox.Show(
                    "管理客户端仅供管理员使用。请使用管理员账号登录，或改用设计客户端。",
                    "无管理权限",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                session.SignOut();
                continue;
            }

            try
            {
                var management = await session.Api.EnterManagementModeAsync();
                session.SetManagementMode(management);
            }
            catch (ApiException ex)
            {
                MessageBox.Show(ex.Message, "进入管理模块失败", MessageBoxButton.OK, MessageBoxImage.Error);
                session.SignOut();
                continue;
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法进入管理模块：" + ex.Message, "进入管理模块失败",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                session.SignOut();
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
