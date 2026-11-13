using System.Windows;
using WinDesign.AdminClient.ViewModels;

namespace WinDesign.AdminClient.Views;

/// <summary>
/// 项目行右键「成员配置」打开的弹窗。
/// 与项目配置页共用 <see cref="ProjectManagementViewModel"/>，保证成员列表和候选用户始终同步。
/// </summary>
public partial class ProjectMemberWindow : Window
{
    public ProjectMemberWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProjectManagementViewModel viewModel)
        {
            // 打开时重新拉取用户与成员，避免使用页面加载时的旧数据。
            await viewModel.ReloadMembersAsync();
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
