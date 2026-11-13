using System.Windows;
using System.Windows.Controls;
using WinDesign.AdminClient.ViewModels;
using WinDesign.Contracts.Projects;

namespace WinDesign.AdminClient.Views;

public partial class ProjectManagementView : UserControl
{
    private GridLength _lastEditorWidth = new(380);

    /// <summary>右键命中的项目行；为空时（表头或空白区域）不弹出菜单。</summary>
    private ProjectDto? _contextMenuProject;

    public ProjectManagementView()
    {
        InitializeComponent();
    }

    /// <summary>右键先选中命中的项目行，再显示行菜单。</summary>
    private void OnProjectsGridPreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _contextMenuProject = null;
        if (sender is not DataGrid grid)
        {
            return;
        }

        if (ItemsControl.ContainerFromElement(grid, e.OriginalSource as DependencyObject) is DataGridRow row &&
            row.Item is ProjectDto project)
        {
            grid.SelectedItem = project;
            _contextMenuProject = project;
        }
    }

    private void OnProjectsGridContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (_contextMenuProject is null)
        {
            e.Handled = true;
        }
    }

    private void OnConfigureMembersClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ProjectManagementViewModel viewModel || viewModel.SelectedProject is null)
        {
            return;
        }

        var dialog = new ProjectMemberWindow
        {
            DataContext = viewModel,
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();
    }

    private void OnToggleEditorClick(object sender, RoutedEventArgs e)
    {
        if (EditorColumn.Width.Value > 1)
        {
            CollapseEditor();
        }
        else
        {
            ExpandEditor();
        }

        UpdateToggleText();
    }

    private void OnNewProjectClick(object sender, RoutedEventArgs e) => ExpandEditor();

    private void CollapseEditor()
    {
        if (EditorColumn.Width.Value > 1)
        {
            _lastEditorWidth = EditorColumn.Width;
        }

        EditorColumn.Width = new GridLength(0);
        EditorPanel.Visibility = Visibility.Collapsed;
    }

    private void ExpandEditor()
    {
        if (EditorColumn.Width.Value <= 1)
        {
            EditorColumn.Width = _lastEditorWidth.Value > 1
                ? _lastEditorWidth
                : new GridLength(380);
        }

        EditorPanel.Visibility = Visibility.Visible;
        UpdateToggleText();
    }

    /// <summary>宽度变为 0 时（包括拖动分隔条到边界）同步收起面板；展开时同步显示。</summary>
    private void OnEditorPanelSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var target = EditorColumn.Width.Value > 1 ? Visibility.Visible : Visibility.Collapsed;
        if (EditorPanel.Visibility != target)
        {
            EditorPanel.Visibility = target;
        }

        UpdateToggleText();
    }

    private void OnEditorLostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        => EditorAutoSave.OnLostKeyboardFocus(sender, e);

    private void UpdateToggleText()
    {
        if (ToggleEditorButton is null)
        {
            return;
        }

        ToggleEditorButton.Content = EditorColumn.Width.Value > 1 ? "收起编辑" : "展开编辑";
    }
}
