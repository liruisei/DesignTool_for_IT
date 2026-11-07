using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinDesign.Client.ViewModels;

namespace WinDesign.Client;

public partial class MainWindow : Window
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
            "WinDesign 设计平台\n仪表 / 电信 / IT 一体化设计\n\n第一阶段：项目信息、工厂 PBS、索引表",
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
