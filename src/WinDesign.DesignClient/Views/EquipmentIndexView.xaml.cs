using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using WinDesign.Client.Services;
using WinDesign.DesignClient.ViewModels;
using WinDesign.Contracts.Equipment;

namespace WinDesign.DesignClient.Views;

public partial class EquipmentIndexView : UserControl
{
    /// <summary>
    /// 嵌入模式：去掉页面外边距与卡片边框/底色，用于嵌在其他模块内（如信息设计下方）。
    /// 独立标签页保持默认值 false。
    /// </summary>
    public static readonly DependencyProperty IsEmbeddedProperty =
        DependencyProperty.Register(
            nameof(IsEmbedded), typeof(bool), typeof(EquipmentIndexView),
            new PropertyMetadata(false));

    private readonly Dictionary<DataGridColumn, string> _columnFilters = new();
    private readonly Dictionary<DataGridColumn, PropertyInfo?> _columnProperties = new();
    private readonly HashSet<ComboBox> _hookedFilterBoxes = new();
    /// <summary>索引表全部数据列，按冻结数量在两个 DataGrid 之间移动。</summary>
    private readonly List<DataGridColumn> _dataColumns = new();
    private ICollectionView? _equipmentView;
    private ObservableCollection<EquipmentDto>? _equipmentSource;
    private CheckBox? _selectAllCheckBox;
    private ScrollViewer? _frozenScroll;
    private ScrollViewer? _equipmentScroll;
    private bool _syncingScroll;
    private bool _syncingSelection;
    private DataGridColumn? _checkBoxColumn;
    private bool _applyingFrozenColumns;

    public EquipmentIndexView()
    {
        InitializeComponent();

        // 恢复当前账号的冻结列数与列顺序；再按冻结数量把列分配到冻结区和滚动区。
        FrozenColumnCountBox.SelectedIndex = Math.Clamp(
            ViewPreferencesService.IndexTableFrozenColumns, 0, FrozenColumnCountBox.Items.Count - 1);

        _checkBoxColumn = FrozenEquipmentGrid.Columns.Count > 0
            ? FrozenEquipmentGrid.Columns[0]
            : null;
        _dataColumns.AddRange(EquipmentGrid.Columns);
        ApplySavedColumnOrder(_dataColumns, ViewPreferencesService.IndexTableColumnOrder);
        EquipmentGrid.Columns.Clear();
        ApplyFrozenColumnCount(FrozenColumnCountBox.SelectedIndex);

        DataContextChanged += OnDataContextChanged;
    }

    public bool IsEmbedded
    {
        get => (bool)GetValue(IsEmbeddedProperty);
        set => SetValue(IsEmbeddedProperty, value);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_equipmentView is not null)
        {
            _equipmentView.CollectionChanged -= OnEquipmentViewChanged;
        }

        if (e.NewValue is not EquipmentIndexViewModel viewModel)
        {
            _equipmentView = null;
            _equipmentSource = null;
            return;
        }

        _equipmentSource = viewModel.Equipments;

        // 逐列筛选挂在 DataGrid 默认的集合视图上：与点击表头排序、服务端过滤共用同一个视图。
        _equipmentView = CollectionViewSource.GetDefaultView(viewModel.Equipments);
        _equipmentView.Filter = MatchesColumnFilters;
        _equipmentView.CollectionChanged += OnEquipmentViewChanged;
        UpdateSelectAllState();
    }

    private void OnEquipmentViewChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => UpdateSelectAllState();

    /// <summary>
    /// 可编辑下拉框的文本变化走 TextBoxBase.TextChanged 路由事件（ComboBox 本身没有 TextChanged）。
    /// 与信息设计页使用同一套筛选交互。
    /// </summary>
    private void OnColumnFilterComboLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox combo)
        {
            return;
        }

        if (_hookedFilterBoxes.Add(combo))
        {
            combo.AddHandler(
                TextBoxBase.TextChangedEvent,
                new TextChangedEventHandler(OnColumnFilterTextChanged),
                handledEventsToo: true);
        }

        RefreshFilterOptions(combo);
    }

    private void OnColumnFilterDropDownOpened(object? sender, EventArgs e)
    {
        if (sender is ComboBox combo)
        {
            RefreshFilterOptions(combo);
        }
    }

    private void OnColumnFilterDropDownClosed(object? sender, EventArgs e)
    {
        if (sender is ComboBox { Tag: DataGridColumn column } combo)
        {
            ApplyColumnFilter(column, combo.Text);
        }
    }

    private void OnColumnFilterTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is ComboBox { Tag: DataGridColumn column } combo)
        {
            ApplyColumnFilter(column, combo.Text);
        }
    }

    private void ApplyColumnFilter(DataGridColumn column, string? text)
    {
        text = text?.Trim() ?? string.Empty;
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

    /// <summary>下拉列出该列全部已有值（不受当前筛选影响），也可继续手工输入。</summary>
    private void RefreshFilterOptions(ComboBox combo)
    {
        if (combo.Tag is not DataGridColumn column
            || GetColumnProperty(column) is not { } property
            || _equipmentSource is null)
        {
            return;
        }

        var values = _equipmentSource
            .Select(row => property.GetValue(row)?.ToString() ?? string.Empty)
            .Where(text => text.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(text => text, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var currentText = combo.Text;
        var options = new List<string> { string.Empty };
        options.AddRange(values);
        combo.ItemsSource = options;
        combo.Text = currentText;
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
    /// 多选完成后，普通单击某一行不应把勾选集合折叠成单选。
    /// 复选框自己的 Preview 处理器负责切换，这里在 DataGrid 处理单元格选择之前拦截；
    /// 表头、滚动条和空白区域仍走默认逻辑（点击空白可清空选择）。
    /// </summary>
    private void OnGridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid || grid.SelectedItems.Count <= 1)
        {
            return;
        }

        // 点击来自复选框时放行，交给 OnRowCheckBoxPreviewMouseLeftButtonDown 切换。
        if (FindAncestor<CheckBox>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        // 只拦截数据行；表头、滚动条和空白区域保持 DataGrid 默认行为。
        if (ItemsControl.ContainerFromElement(grid, e.OriginalSource as DependencyObject) is not DataGridRow)
        {
            return;
        }

        e.Handled = true;
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

    private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T target)
            {
                return target;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
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
        if (_syncingSelection)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            SyncSelectedEquipments(EquipmentGrid, FrozenEquipmentGrid);
            if (DataContext is EquipmentIndexViewModel viewModel)
            {
                // 勾选框绑定 DataGridRow.IsSelected，多选状态统一由 ViewModel 镜像。
                viewModel.UpdateSelection(EquipmentGrid.SelectedItems.OfType<EquipmentDto>());
            }
        }
        finally
        {
            _syncingSelection = false;
        }

        UpdateSelectAllState();
    }

    private void OnFrozenGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSelection)
        {
            return;
        }

        _syncingSelection = true;
        try
        {
            SyncSelectedEquipments(FrozenEquipmentGrid, EquipmentGrid);
            if (DataContext is EquipmentIndexViewModel viewModel)
            {
                viewModel.UpdateSelection(FrozenEquipmentGrid.SelectedItems.OfType<EquipmentDto>());
            }
        }
        finally
        {
            _syncingSelection = false;
        }

        UpdateSelectAllState();
    }

    private static void SyncSelectedEquipments(DataGrid source, DataGrid target)
    {
        var selected = source.SelectedItems.Cast<object>().ToList();
        target.SelectedItems.Clear();
        foreach (var item in selected)
        {
            target.SelectedItems.Add(item);
        }
    }

    // ---------- 冻结列配置 ----------

    private void OnFreezeSettingsClick(object sender, RoutedEventArgs e)
    {
        FreezeSettingsPanel.Visibility = FreezeSettingsPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OnFrozenColumnCountChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_dataColumns.Count == 0)
        {
            return;
        }

        var count = Math.Clamp(FrozenColumnCountBox.SelectedIndex, 0, _dataColumns.Count);
        // 配置即保存，下次启动自动恢复。
        ViewPreferencesService.IndexTableFrozenColumns = count;
        ApplyFrozenColumnCount(count);
    }

    /// <summary>把前 count 个数据列放进冻结区，其余留在可横向滚动的表格中。</summary>
    private void ApplyFrozenColumnCount(int count)
    {
        if (_dataColumns.Count == 0)
        {
            return;
        }

        var clamped = Math.Clamp(count, 0, _dataColumns.Count);

        _applyingFrozenColumns = true;
        try
        {
            // 冻结区第 0 列始终是勾选框，先移除之前移入的数据列。
            while (FrozenEquipmentGrid.Columns.Count > 1)
            {
                FrozenEquipmentGrid.Columns.RemoveAt(1);
            }
            EquipmentGrid.Columns.Clear();

            for (var i = 0; i < _dataColumns.Count; i++)
            {
                if (i < clamped)
                {
                    FrozenEquipmentGrid.Columns.Add(_dataColumns[i]);
                }
                else
                {
                    EquipmentGrid.Columns.Add(_dataColumns[i]);
                }
            }
        }
        finally
        {
            _applyingFrozenColumns = false;
        }

        // 列移动后表头筛选控件会重建，清掉旧筛选值，避免出现“看不见仍在生效”的筛选。
        _columnFilters.Clear();
        _equipmentView?.Refresh();
    }

    // ---------- 列顺序 ----------

    private void OnEquipmentColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        if (_applyingFrozenColumns)
        {
            return;
        }

        SaveEquipmentColumnOrder();
    }

    private void OnEquipmentColumnReordering(object? sender, DataGridColumnReorderingEventArgs e)
    {
        // 勾选列固定在冻结区第一位，不允许被拖走。
        if (ReferenceEquals(e.Column, _checkBoxColumn))
        {
            e.Cancel = true;
        }
    }

    private void SaveEquipmentColumnOrder()
    {
        var ordered = FrozenEquipmentGrid.Columns.Cast<DataGridColumn>()
            .Where(x => !ReferenceEquals(x, _checkBoxColumn))
            .Concat(EquipmentGrid.Columns.Cast<DataGridColumn>())
            .ToList();

        _dataColumns.Clear();
        _dataColumns.AddRange(ordered);
        ViewPreferencesService.IndexTableColumnOrder = ordered.Select(ColumnKey).ToList();
    }

    private static void ApplySavedColumnOrder(
        List<DataGridColumn> columns,
        IReadOnlyList<string>? savedOrder)
    {
        if (savedOrder is null || savedOrder.Count == 0)
        {
            return;
        }

        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < savedOrder.Count; i++)
        {
            indexes[savedOrder[i]] = i;
        }

        var ordered = columns
            .Select((column, index) => (Column: column, Index: index, Key: ColumnKey(column)))
            .OrderBy(x => indexes.TryGetValue(x.Key, out var order) ? order : int.MaxValue)
            .ThenBy(x => x.Index)
            .Select(x => x.Column)
            .ToList();

        columns.Clear();
        columns.AddRange(ordered);
    }

    private static string ColumnKey(DataGridColumn column) =>
        column.Header?.ToString() ?? string.Empty;

    // ---------- 两个表格的纵向滚动同步 ----------

    private void OnEquipmentGridsLoaded(object sender, RoutedEventArgs e)
    {
        if (_frozenScroll is null)
        {
            _frozenScroll = FindVisualChild<ScrollViewer>(FrozenEquipmentGrid);
            if (_frozenScroll is not null)
            {
                _frozenScroll.ScrollChanged += OnFrozenScrollChanged;
            }
        }

        if (_equipmentScroll is null)
        {
            _equipmentScroll = FindVisualChild<ScrollViewer>(EquipmentGrid);
            if (_equipmentScroll is not null)
            {
                _equipmentScroll.ScrollChanged += OnEquipmentScrollChanged;
            }
        }
    }

    private void OnEquipmentScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_syncingScroll || _frozenScroll is null || e.VerticalChange == 0)
        {
            return;
        }

        _syncingScroll = true;
        try
        {
            _frozenScroll.ScrollToVerticalOffset(e.VerticalOffset);
        }
        finally
        {
            _syncingScroll = false;
        }
    }

    private void OnFrozenScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_syncingScroll || _equipmentScroll is null || e.VerticalChange == 0)
        {
            return;
        }

        _syncingScroll = true;
        try
        {
            _equipmentScroll.ScrollToVerticalOffset(e.VerticalOffset);
        }
        finally
        {
            _syncingScroll = false;
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            var descendant = FindVisualChild<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private void OnSelectAllCheckBoxLoaded(object sender, RoutedEventArgs e)
    {
        _selectAllCheckBox = sender as CheckBox;
        UpdateSelectAllState();
    }

    /// <summary>
    /// 表头全选框：未全选时一次选中当前列表的全部行，已全选时取消全选。
    /// 拦截 Preview 事件，避免三态复选框自己被切到下一个状态。
    /// </summary>
    private void OnSelectAllCheckBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        var items = EquipmentGrid.Items.Cast<object>().ToList();
        if (items.Count == 0)
        {
            return;
        }

        var selected = new HashSet<object>(EquipmentGrid.SelectedItems.Cast<object>());
        if (items.All(selected.Contains))
        {
            EquipmentGrid.UnselectAll();
        }
        else
        {
            foreach (var item in items)
            {
                if (!selected.Contains(item))
                {
                    EquipmentGrid.SelectedItems.Add(item);
                }
            }
        }

        UpdateSelectAllState();
    }

    /// <summary>根据当前列表（含列筛选后的可见行）回显表头全选框的三态。</summary>
    private void UpdateSelectAllState()
    {
        if (_selectAllCheckBox is null)
        {
            return;
        }

        var total = EquipmentGrid.Items.Count;
        if (total == 0)
        {
            _selectAllCheckBox.IsChecked = false;
            _selectAllCheckBox.IsEnabled = false;
            return;
        }

        var selected = new HashSet<object>(EquipmentGrid.SelectedItems.Cast<object>());
        var selectedCount = EquipmentGrid.Items.Cast<object>().Count(selected.Contains);

        _selectAllCheckBox.IsEnabled = true;
        _selectAllCheckBox.IsChecked = selectedCount == 0
            ? false
            : selectedCount >= total
                ? true
                : null;
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
