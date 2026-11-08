using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinDesign.Client.ViewModels;

namespace WinDesign.Client.Views;

public partial class WorkspaceSelectionWindow : Window
{
    private WorkspaceSelectionViewModel? _viewModel;

    public WorkspaceSelectionWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.SelectionSucceeded -= OnSelectionSucceeded;
        }

        _viewModel = DataContext as WorkspaceSelectionViewModel;
        if (_viewModel is not null)
        {
            _viewModel.SelectionSucceeded += OnSelectionSucceeded;
        }
    }

    private void OnSelectionSucceeded(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    /// <summary>
    /// 双击项目直接进入工作区；双击空白区域或滚动条不触发。
    /// 用 ContainerFromElement 判断，兼容 OriginalSource 是 Run 等 ContentElement 的情况。
    /// </summary>
    private void OnProjectListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not WorkspaceSelectionViewModel viewModel)
        {
            return;
        }
        if (sender is not ListBox listBox ||
            ItemsControl.ContainerFromElement(listBox, e.OriginalSource as DependencyObject) is null)
        {
            return;
        }
        if (viewModel.SelectedProject is null || !viewModel.ConfirmCommand.CanExecute(null))
        {
            return;
        }

        viewModel.ConfirmCommand.Execute(null);
    }
}
