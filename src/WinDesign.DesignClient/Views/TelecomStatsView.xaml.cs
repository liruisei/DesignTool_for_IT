using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using WinDesign.DesignClient.ViewModels;

namespace WinDesign.DesignClient.Views;

public partial class TelecomStatsView : UserControl
{
    private readonly Dictionary<DataGridColumn, string> _columnFilters = new();
    private readonly Dictionary<DataGridColumn, PropertyInfo?> _columnProperties = new();
    private readonly HashSet<ComboBox> _hookedFilterBoxes = new();
    private ICollectionView? _roomsView;
    private ObservableCollection<TelecomRoomRowViewModel>? _roomsSource;

    public TelecomStatsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
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
    }

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
    /// DataGrid.SelectedItems 不能直接绑定，因此在这里把多选结果回填给 ViewModel，
    /// 供右键删除使用。
    /// </summary>
    private void OnRoomSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is TelecomStatsViewModel viewModel && sender is DataGrid grid)
        {
            viewModel.SetSelection(grid.SelectedItems.OfType<TelecomRoomRowViewModel>());
        }
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
