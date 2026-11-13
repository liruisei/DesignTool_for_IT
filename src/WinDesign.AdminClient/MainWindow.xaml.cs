using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WinDesign.AdminClient.ViewModels;
using WinDesign.Client.ViewModels;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;

namespace WinDesign.AdminClient;

public partial class MainWindow : FluentWindow
{
    private const double DefaultExplorerWidth = 250;
    private GridLength _lastExplorerWidth = new(DefaultExplorerWidth);
    private double _splitterDragDistance;
    private bool _splitterDragged;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnExplorerSplitterDragStarted(object sender, DragStartedEventArgs e)
    {
        _splitterDragDistance = 0;
        _splitterDragged = false;
    }

    /// <summary>记录实际拖动距离；隔断会跟随鼠标移动，不能用鼠标相对位置判断。</summary>
    private void OnExplorerSplitterDragDelta(object sender, DragDeltaEventArgs e)
    {
        _splitterDragDistance += Math.Abs(e.HorizontalChange);
        if (_splitterDragDistance > 2)
        {
            _splitterDragged = true;
        }
    }

    /// <summary>单击隔断切换收起/展开；真正拖动过则只调整宽度，不切换。</summary>
    private void OnExplorerSplitterPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_splitterDragged)
        {
            ToggleExplorer();
        }

        _splitterDragged = false;
    }

    /// <summary>一键收起/恢复左侧 Explorer，恢复时使用收起前的宽度。</summary>
    private void ToggleExplorer()
    {
        // 用 Width 而不是 ActualWidth：同一次鼠标事件里 ActualWidth 尚未随布局更新。
        var isExpanded = ExplorerColumn.Width.Value > 0;
        if (isExpanded)
        {
            if (ExplorerColumn.Width.IsAbsolute && ExplorerColumn.Width.Value > 0)
            {
                _lastExplorerWidth = ExplorerColumn.Width;
            }
            ExplorerColumn.Width = new GridLength(0);
        }
        else
        {
            ExplorerColumn.Width = _lastExplorerWidth.Value >= 80
                ? _lastExplorerWidth
                : new GridLength(DefaultExplorerWidth);
        }

        UpdateExplorerToggleState(!isExpanded);
    }

    /// <summary>拖拽分隔条后同步按钮方向与记录的宽度。</summary>
    private void OnExplorerSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        // 点击切换也会触发 DragCompleted；以 Width 为准，避免把刚收起的列误判为展开。
        var isExpanded = ExplorerColumn.Width.Value > 0;
        if (isExpanded && ExplorerColumn.Width.IsAbsolute)
        {
            _lastExplorerWidth = ExplorerColumn.Width;
        }
        UpdateExplorerToggleState(isExpanded);
    }

    private void UpdateExplorerToggleState(bool isExpanded)
    {
        ExplorerToggleGlyph.Text = isExpanded ? "‹" : "›";
        ExplorerSplitter.ToolTip = isExpanded
            ? "单击收起左侧面板；拖动可调整宽度"
            : "单击展开左侧面板";
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
