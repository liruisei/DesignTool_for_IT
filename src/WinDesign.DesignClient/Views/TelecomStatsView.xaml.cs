using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using WinDesign.Client.Services;
using WinDesign.DesignClient.ViewModels;

namespace WinDesign.DesignClient.Views;

public partial class TelecomStatsView : UserControl
{
    private readonly Dictionary<DataGridColumn, string> _columnFilters = new();
    private readonly Dictionary<DataGridColumn, PropertyInfo?> _columnProperties = new();
    private readonly HashSet<ComboBox> _hookedFilterBoxes = new();
    /// <summary>房间明细的全部数据列，按冻结数量在两个 DataGrid 之间移动。</summary>
    private readonly List<DataGridColumn> _roomDataColumns = new();
    private ICollectionView? _roomsView;
    private ObservableCollection<TelecomRoomRowViewModel>? _roomsSource;
    private CheckBox? _selectAllCheckBox;
    private ScrollViewer? _frozenScroll;
    private ScrollViewer? _roomScroll;
    private bool _syncingScroll;
    private bool _syncingRoomSelection;
    private DataGridColumn? _checkBoxColumn;
    private bool _applyingFrozenColumns;

    public TelecomStatsView()
    {
        InitializeComponent();

        // 恢复当前账号的冻结列数与列顺序；再按冻结数量把列分配到冻结区和滚动区。
        FrozenColumnCountBox.SelectedIndex = Math.Clamp(
            ViewPreferencesService.RoomTableFrozenColumns, 0, FrozenColumnCountBox.Items.Count - 1);

        _checkBoxColumn = FrozenRoomGrid.Columns.Count > 0
            ? FrozenRoomGrid.Columns[0]
            : null;
        _roomDataColumns.AddRange(RoomGrid.Columns);
        ApplySavedColumnOrder(_roomDataColumns, ViewPreferencesService.RoomTableColumnOrder);
        RoomGrid.Columns.Clear();
        ApplyFrozenColumnCount(FrozenColumnCountBox.SelectedIndex);

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_roomsView is not null)
        {
            _roomsView.CollectionChanged -= OnRoomsViewChanged;
        }

        if (e.NewValue is not TelecomStatsViewModel viewModel)
        {
            _roomsView = null;
            _roomsSource = null;
            return;
        }

        // 逐列筛选挂在 DataGrid 默认集合视图上：与点击表头排序共用同一个视图。
        _roomsSource = viewModel.Rooms;
        _roomsView = CollectionViewSource.GetDefaultView(viewModel.Rooms);
        _roomsView.Filter = MatchesColumnFilters;
        _roomsView.CollectionChanged += OnRoomsViewChanged;
        UpdateSelectAllState();
    }

    private void OnRoomsViewChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => UpdateSelectAllState();

    /// <summary>
    /// 可编辑下拉框的文本变化走 TextBoxBase.TextChanged 路由事件（ComboBox 本身没有 TextChanged）。
    /// </summary>
    private void OnRoomFilterComboLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ComboBox combo)
        {
            return;
        }

        if (_hookedFilterBoxes.Add(combo))
        {
            combo.AddHandler(
                TextBoxBase.TextChangedEvent,
                new TextChangedEventHandler(OnRoomFilterTextChanged),
                handledEventsToo: true);
        }

        RefreshFilterOptions(combo);
    }

    private void OnRoomFilterDropDownOpened(object? sender, EventArgs e)
    {
        if (sender is ComboBox combo)
        {
            RefreshFilterOptions(combo);
        }
    }

    private void OnRoomFilterDropDownClosed(object? sender, EventArgs e)
    {
        if (sender is ComboBox { Tag: DataGridColumn column } combo)
        {
            ApplyColumnFilter(column, combo.Text);
        }
    }

    private void OnRoomFilterTextChanged(object sender, TextChangedEventArgs e)
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

        _roomsView?.Refresh();
    }

    /// <summary>下拉列出该列全部已有值（不受当前筛选影响），也可继续手工输入。</summary>
    private void RefreshFilterOptions(ComboBox combo)
    {
        if (combo.Tag is not DataGridColumn column
            || GetColumnProperty(column) is not { } property
            || _roomsSource is null)
        {
            return;
        }

        var values = _roomsSource
            .Select(row => property.GetValue(row)?.ToString() ?? string.Empty)
            .Where(text => text.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        // 数字列按数值排序，其余按文本排序；首项留空用于清除筛选。
        if (property.PropertyType == typeof(int))
        {
            values = values
                .OrderBy(text => int.TryParse(text, out var number) ? number : int.MaxValue)
                .ToList();
        }
        else
        {
            values = values.OrderBy(text => text, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        var currentText = combo.Text;
        var options = new List<string> { string.Empty };
        options.AddRange(values);
        combo.ItemsSource = options;
        combo.Text = currentText;
    }

    /// <summary>各列筛选为“与”关系，不区分大小写的包含匹配。</summary>
    private bool MatchesColumnFilters(object item)
    {
        if (_columnFilters.Count == 0 || item is not TelecomRoomRowViewModel row)
        {
            return true;
        }

        foreach (var (column, text) in _columnFilters)
        {
            var value = GetColumnProperty(column)?.GetValue(row)?.ToString() ?? string.Empty;
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
            : typeof(TelecomRoomRowViewModel).GetProperty(path);
        _columnProperties[column] = property;
        return property;
    }

    /// <summary>
    /// 冻结区与滚动区是两个 DataGrid：把选中行同步到另一侧。
    /// DataGrid.SelectedItems 不能直接绑定，因此由触发变化的一侧回填 ViewModel。
    /// </summary>
    private void OnRoomSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingRoomSelection)
        {
            return;
        }

        _syncingRoomSelection = true;
        try
        {
            SyncSelectedRooms(RoomGrid, FrozenRoomGrid);
            if (DataContext is TelecomStatsViewModel viewModel)
            {
                viewModel.SetSelection(RoomGrid.SelectedItems.OfType<TelecomRoomRowViewModel>());
            }
        }
        finally
        {
            _syncingRoomSelection = false;
        }

        UpdateSelectAllState();
    }

    private void OnFrozenRoomSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingRoomSelection)
        {
            return;
        }

        _syncingRoomSelection = true;
        try
        {
            SyncSelectedRooms(FrozenRoomGrid, RoomGrid);
            if (DataContext is TelecomStatsViewModel viewModel)
            {
                viewModel.SetSelection(FrozenRoomGrid.SelectedItems.OfType<TelecomRoomRowViewModel>());
            }
        }
        finally
        {
            _syncingRoomSelection = false;
        }

        UpdateSelectAllState();
    }

    private static void SyncSelectedRooms(DataGrid source, DataGrid target)
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
        if (_roomDataColumns.Count == 0)
        {
            return;
        }

        var count = Math.Clamp(FrozenColumnCountBox.SelectedIndex, 0, _roomDataColumns.Count);
        // 配置即保存，下次启动自动恢复。
        ViewPreferencesService.RoomTableFrozenColumns = count;
        ApplyFrozenColumnCount(count);
    }

    /// <summary>把前 count 个数据列放进冻结区，其余留在可横向滚动的表格中。</summary>
    private void ApplyFrozenColumnCount(int count)
    {
        if (_roomDataColumns.Count == 0)
        {
            return;
        }

        var clamped = Math.Clamp(count, 0, _roomDataColumns.Count);

        _applyingFrozenColumns = true;
        try
        {
            // 冻结区第 0 列始终是勾选框，先移除之前移入的数据列。
            while (FrozenRoomGrid.Columns.Count > 1)
            {
                FrozenRoomGrid.Columns.RemoveAt(1);
            }
            RoomGrid.Columns.Clear();

            for (var i = 0; i < _roomDataColumns.Count; i++)
            {
                if (i < clamped)
                {
                    FrozenRoomGrid.Columns.Add(_roomDataColumns[i]);
                }
                else
                {
                    RoomGrid.Columns.Add(_roomDataColumns[i]);
                }
            }
        }
        finally
        {
            _applyingFrozenColumns = false;
        }

        // 列移动后表头筛选控件会重建，清掉旧筛选值，避免出现“看不见仍在生效”的筛选。
        _columnFilters.Clear();
        _roomsView?.Refresh();
    }

    // ---------- 列顺序 ----------

    private void OnRoomColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        if (_applyingFrozenColumns)
        {
            return;
        }

        SaveRoomColumnOrder();
    }

    private void OnRoomColumnReordering(object? sender, DataGridColumnReorderingEventArgs e)
    {
        // 勾选列固定在冻结区第一位，不允许被拖走。
        if (ReferenceEquals(e.Column, _checkBoxColumn))
        {
            e.Cancel = true;
        }
    }

    private void SaveRoomColumnOrder()
    {
        var ordered = FrozenRoomGrid.Columns.Cast<DataGridColumn>()
            .Where(x => !ReferenceEquals(x, _checkBoxColumn))
            .Concat(RoomGrid.Columns.Cast<DataGridColumn>())
            .ToList();

        _roomDataColumns.Clear();
        _roomDataColumns.AddRange(ordered);
        ViewPreferencesService.RoomTableColumnOrder = ordered.Select(ColumnKey).ToList();
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

    private void OnRoomGridsLoaded(object sender, RoutedEventArgs e)
    {
        if (_frozenScroll is null)
        {
            _frozenScroll = FindVisualChild<ScrollViewer>(FrozenRoomGrid);
            if (_frozenScroll is not null)
            {
                _frozenScroll.ScrollChanged += OnFrozenScrollChanged;
            }
        }

        if (_roomScroll is null)
        {
            _roomScroll = FindVisualChild<ScrollViewer>(RoomGrid);
            if (_roomScroll is not null)
            {
                _roomScroll.ScrollChanged += OnRoomScrollChanged;
            }
        }
    }

    private void OnRoomScrollChanged(object sender, ScrollChangedEventArgs e)
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
        if (_syncingScroll || _roomScroll is null || e.VerticalChange == 0)
        {
            return;
        }

        _syncingScroll = true;
        try
        {
            _roomScroll.ScrollToVerticalOffset(e.VerticalOffset);
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

    /// <summary>
    /// 与索引表一致：多选完成后，普通单击行不折叠勾选结果；复选框点击仍交给自己的处理器。
    /// </summary>
    private void OnRoomGridPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid || grid.SelectedItems.Count <= 1)
        {
            return;
        }

        if (FindAncestor<CheckBox>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        if (ItemsControl.ContainerFromElement(grid, e.OriginalSource as DependencyObject) is not DataGridRow)
        {
            return;
        }

        e.Handled = true;
    }

    private void OnSelectAllCheckBoxLoaded(object sender, RoutedEventArgs e)
    {
        _selectAllCheckBox = sender as CheckBox;
        UpdateSelectAllState();
    }

    /// <summary>表头全选框：未全选时选中当前列表的全部行，已全选时取消全选。</summary>
    private void OnSelectAllCheckBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        var items = RoomGrid.Items.Cast<object>().ToList();
        if (items.Count == 0)
        {
            return;
        }

        var selected = new HashSet<object>(RoomGrid.SelectedItems.Cast<object>());
        if (items.All(selected.Contains))
        {
            RoomGrid.UnselectAll();
        }
        else
        {
            foreach (var item in items)
            {
                if (!selected.Contains(item))
                {
                    RoomGrid.SelectedItems.Add(item);
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

        var total = RoomGrid.Items.Count;
        if (total == 0)
        {
            _selectAllCheckBox.IsChecked = false;
            _selectAllCheckBox.IsEnabled = false;
            return;
        }

        var selected = new HashSet<object>(RoomGrid.SelectedItems.Cast<object>());
        var selectedCount = RoomGrid.Items.Cast<object>().Count(selected.Contains);

        _selectAllCheckBox.IsEnabled = true;
        _selectAllCheckBox.IsChecked = selectedCount == 0
            ? false
            : selectedCount >= total
                ? true
                : null;
    }

    /// <summary>
    /// 右键落在未选中的行上时，先把选择切换到该行（随后菜单删除单行）；
    /// 右键已在多选内的行时保留多选，菜单会删除整组。
    /// 这样菜单文字和实际删除范围始终一致。
    /// </summary>
    private void OnRoomRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return;
        }

        var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row is null || row.IsSelected)
        {
            return;
        }

        grid.SelectedItems.Clear();
        row.IsSelected = true;
    }

    /// <summary>
    /// DataGrid 的单元格选中逻辑会吞掉第一次单击，导致复选框点不动。
    /// 在 Preview 阶段直接切换行选中并标记已处理，视觉状态由 IsChecked 绑定回显。
    /// </summary>
    private void OnRoomCheckBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is CheckBox && FindAncestor<DataGridRow>(sender as DependencyObject) is { } row)
        {
            row.IsSelected = !row.IsSelected;
            e.Handled = true;
        }
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
