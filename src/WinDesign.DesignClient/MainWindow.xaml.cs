using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WinDesign.Client.ViewModels;
using WinDesign.DesignClient.ViewModels;
using WinDesign.DesignClient.Views;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;

namespace WinDesign.DesignClient;

public partial class MainWindow : FluentWindow
{
    private const double DefaultExplorerWidth = 250;
    private MainViewModel? _viewModel;
    private GridLength _lastExplorerWidth = new(DefaultExplorerWidth);

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private double _splitterDragDistance;
    private bool _splitterDragged;

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

        var workspaceViewModel = new WorkspaceSelectionViewModel(
            session, session.PlantId, session.ProjectId, isSwitching: true);
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

    private void OnProjectExplorerSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel viewModel && e.NewValue is ExplorerNode node)
        {
            viewModel.SelectedProjectNode = node;
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
    private void OnProjectExplorerMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var item = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
        if (item?.DataContext is ExplorerNode { Open: not null } node)
        {
            viewModel.SelectedProjectNode = node;
            viewModel.OpenExplorerNode(node);
            e.Handled = true;
        }
    }

    private void OnPbsExplorerSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel viewModel && e.NewValue is ExplorerNode node)
        {
            // PBS 面板只读：选中仅更新状态，不打开文档。
            viewModel.SelectedPbsNode = node;
        }
    }

    private void OnProjectExplorerContextMenuOpening(object sender, ContextMenuEventArgs e) =>
        PrepareExplorerContextMenu(sender, e, isPbs: false);

    private void OnPbsExplorerContextMenuOpening(object sender, ContextMenuEventArgs e) =>
        PrepareExplorerContextMenu(sender, e, isPbs: true);

    private static void PrepareExplorerContextMenu(object sender, ContextMenuEventArgs e, bool isPbs)
    {
        if (sender is not TreeView tree)
        {
            return;
        }

        var item = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
        if (item?.DataContext is ExplorerNode node && tree.DataContext is MainViewModel viewModel)
        {
            if (isPbs)
            {
                viewModel.SelectedPbsNode = node;
            }
            else
            {
                viewModel.SelectedProjectNode = node;
            }

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
            "工厂二维设计平台 - 设计客户端\nPlant 2D Design Platform\n工艺 / 仪表 / 电信 / IT 二维设计\n\n工厂（PBS 根节点）→ 项目 → 索引表 / 规格书 / 材料表 / 报表",
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
