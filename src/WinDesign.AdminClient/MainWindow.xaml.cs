using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinDesign.AdminClient.ViewModels;
using WinDesign.Client.ViewModels;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;

namespace WinDesign.AdminClient;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
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

    /// <summary>
    /// 双击已选中的节点时也能重新打开文档：关闭全部标签后树项仍处于选中状态，
    /// 不会再次触发 SelectedItemChanged。
    /// </summary>
    private void OnExplorerMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var item = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
        if (item?.DataContext is ExplorerNode { Open: not null } node)
        {
            viewModel.SelectedExplorerNode = node;
            viewModel.OpenExplorerNode(node);
            e.Handled = true;
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
            "工厂二维设计平台 - 管理客户端\nPlant 2D Design Platform\n工艺 / 仪表 / 电信 / IT 二维设计\n系统配置：工厂 / 项目 / PBS / 编码 / 用户\n\n管理客户端不绑定工厂与项目，可跨工厂维护配置。",
            "关于 工厂二维设计平台",
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
