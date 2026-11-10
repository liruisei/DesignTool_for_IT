using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinDesign.DesignClient.ViewModels;

namespace WinDesign.DesignClient.Views;

public partial class EquipmentSpecIndexView : UserControl
{
    public EquipmentSpecIndexView()
    {
        InitializeComponent();
    }

    private void OnGridMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        // 只有双击数据行才打开；表头分隔线、滚动条和空白区域不响应。
        if (ItemsControl.ContainerFromElement(grid, e.OriginalSource as DependencyObject) is not DataGridRow)
        {
            return;
        }

        if (DataContext is EquipmentSpecIndexViewModel viewModel &&
            viewModel.OpenSpecCommand.CanExecute(null))
        {
            viewModel.OpenSpecCommand.Execute(null);
            e.Handled = true;
        }
    }
}
