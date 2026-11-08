using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinDesign.Client.ViewModels;
using WinDesign.Client.Views;

namespace WinDesign.Client;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.SwitchWorkspaceRequested -= OnSwitchWorkspaceRequested;
        }

        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.SwitchWorkspaceRequested += OnSwitchWorkspaceRequested;
        }
    }

    private async void OnSwitchWorkspaceRequested(object? sender, EventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var session = _viewModel.Session;
        if (!session.HasWorkspace)
        {
            return;
        }

        // 切换前自动保存当前打开文档中的待提交编辑；保存失败则取消切换。
        var saveError = await _viewModel.SavePendingDocumentsAsync();
        if (!string.IsNullOrWhiteSpace(saveError))
        {
            MessageBox.Show(
                "自动保存失败，已取消切换工作区。\n\n" + saveError,
                "切换工作区",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // 复用登录后的工作区选择窗口，并预选当前工厂/项目。
        var workspaceViewModel = new WorkspaceSelectionViewModel(
            session, session.PlantId, session.ProjectId);
        var window = new WorkspaceSelectionWindow
        {
            DataContext = workspaceViewModel,
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            _viewModel.ApplyWorkspaceChange();
        }
    }

    private void OnExplorerSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel viewModel && e.NewValue is ExplorerNode node)
        {
            viewModel.SelectedExplorerNode = node;
            if (node.Open is not null)
            {
                viewModel.OpenExplorerNode(node);
            }
        }
    }

    private void OnExplorerContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not TreeView tree)
        {
            return;
        }

        var item = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
        if (item?.DataContext is ExplorerNode node && tree.DataContext is MainViewModel viewModel)
        {
            viewModel.SelectedExplorerNode = node;
            item.IsSelected = true;
            item.Focus();
        }
        else
        {
            e.Handled = true;
            return;
        }

        if (tree.ContextMenu is not null)
        {
            tree.ContextMenu.DataContext = tree.DataContext;
        }
    }

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "WinDesign 设计平台\n仪表 / 电信 / IT 一体化设计\n\n工厂（PBS 根节点）→ 项目 → PBS 管理 / 索引表 / 项目配置",
            "关于 WinDesign",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T found)
            {
                return found;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
