using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinDesign.AdminClient.ViewModels;

namespace WinDesign.AdminClient.Views;

public partial class PbsView : UserControl
{
    public PbsView()
    {
        InitializeComponent();
    }

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is PbsViewModel viewModel && e.NewValue is PbsNodeItem item)
        {
            viewModel.SelectedNode = item;
        }
    }

    private void OnTreeMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is PbsViewModel viewModel && viewModel.EditNodeCommand.CanExecute(null))
        {
            viewModel.EditNodeCommand.Execute(null);
        }
    }

    private void OnTreeContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not TreeView tree)
        {
            return;
        }

        var item = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
        if (item?.DataContext is PbsNodeItem node && tree.DataContext is PbsViewModel viewModel)
        {
            viewModel.SelectedNode = node;
            item.IsSelected = true;
            item.Focus();
        }
        else
        {
            // 空白区域不弹菜单，避免误操作到上一次选中的节点
            e.Handled = true;
            return;
        }

        if (tree.ContextMenu is not null)
        {
            tree.ContextMenu.DataContext = tree.DataContext;
        }
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
