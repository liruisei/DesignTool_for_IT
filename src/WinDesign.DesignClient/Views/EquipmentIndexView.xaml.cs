using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using WinDesign.DesignClient.ViewModels;
using WinDesign.Contracts.Equipment;

namespace WinDesign.DesignClient.Views;

public partial class EquipmentIndexView : UserControl
{
    private readonly Dictionary<DataGridColumn, string> _columnFilters = new();
    private readonly Dictionary<DataGridColumn, PropertyInfo?> _columnProperties = new();
    private ICollectionView? _equipmentView;

    public EquipmentIndexView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not EquipmentIndexViewModel viewModel)
        {
            _equipmentView = null;
            return;
        }

        // 逐列筛选挂在 DataGrid 默认的集合视图上：与点击表头排序、服务端过滤共用同一个视图。
        _equipmentView = CollectionViewSource.GetDefaultView(viewModel.Equipments);
        _equipmentView.Filter = MatchesColumnFilters;
    }

    private void OnColumnFilterTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox { Tag: DataGridColumn column } box)
        {
            return;
        }

        var text = box.Text.Trim();
        if (text.Length == 0)
        {
            _columnFilters.Remove(column);
        }
        else
        {
            _columnFilters[column] = text;
        }

        _equipmentView?.Refresh();
    }

    /// <summary>
    /// 各列筛选为“与”关系：每一列都必须匹配（不区分大小写的包含匹配）才显示该行。
    /// </summary>
    private bool MatchesColumnFilters(object item)
    {
        if (_columnFilters.Count == 0 || item is not EquipmentDto equipment)
        {
            return true;
        }

        foreach (var (column, text) in _columnFilters)
        {
            var value = GetColumnProperty(column)?.GetValue(equipment)?.ToString() ?? string.Empty;
            if (!value.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private PropertyInfo? GetColumnProperty(DataGridColumn column)
    {
        if (_columnProperties.TryGetValue(column, out var cached))
        {
            return cached;
        }

        var path = column.SortMemberPath;
        if (string.IsNullOrWhiteSpace(path)
            && column is DataGridBoundColumn { Binding: Binding binding })
        {
            path = binding.Path?.Path;
        }

        var property = string.IsNullOrWhiteSpace(path)
            ? null
            : typeof(EquipmentDto).GetProperty(path);
        _columnProperties[column] = property;
        return property;
    }

    /// <summary>
    /// DataGrid 的单元格选中逻辑会吞掉第一次单击，导致复选框点不动。
    /// 在 Preview 阶段直接切换行选中并标记已处理，视觉状态由 IsChecked 绑定回显。
    /// </summary>
    private void OnRowCheckBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is CheckBox && FindAncestorRow(sender as DependencyObject) is { } row)
        {
            row.IsSelected = !row.IsSelected;
            e.Handled = true;
        }
    }

    private static DataGridRow? FindAncestorRow(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is DataGridRow row)
            {
                return row;
            }
            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid grid && DataContext is EquipmentIndexViewModel viewModel)
        {
            // 勾选框绑定 DataGridRow.IsSelected，多选状态统一由 ViewModel 镜像。
            viewModel.UpdateSelection(grid.SelectedItems.OfType<EquipmentDto>());
        }
    }

    /// <summary>右键行时先把它设为选中行，右键菜单命令才能作用于正确的位号。</summary>
    private void OnGridPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        if (ItemsControl.ContainerFromElement(grid, e.OriginalSource as DependencyObject) is not DataGridRow row)
        {
            return;
        }

        if (!row.IsSelected)
        {
            grid.SelectedItems.Clear();
            row.IsSelected = true;
        }
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

        if (DataContext is EquipmentIndexViewModel viewModel &&
            viewModel.IsSingleSelection &&
            viewModel.EditEquipmentCommand.CanExecute(null))
        {
            viewModel.EditEquipmentCommand.Execute(null);
            e.Handled = true;
        }
    }
}
