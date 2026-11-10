using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinDesign.AdminClient.ViewModels;

namespace WinDesign.AdminClient.Views;

public partial class DictionaryView : UserControl
{
    private GridLength _lastEditorWidth = new(280);

    public DictionaryView()
    {
        InitializeComponent();
    }

    // 点击编码类型条目：显示类型编辑栏；点击编码项行：显示编码项编辑栏。
    // 两者共用同一列，后打开的直接替换先打开的。
    private void OnTypeListMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is not null)
        {
            ShowTypeEditor();
        }
    }

    private void OnItemGridMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) is not null)
        {
            ShowItemEditor();
        }
    }

    private void OnNewTypeClick(object sender, RoutedEventArgs e) => ShowTypeEditor();

    private void OnNewItemClick(object sender, RoutedEventArgs e)
    {
        // 没有选中编码类型时不能新建编码项，也不弹出编辑栏。
        if (DataContext is DictionaryViewModel { SelectedType: not null })
        {
            ShowItemEditor();
        }
    }

    private void ShowTypeEditor()
    {
        ItemEditorPanel.Visibility = Visibility.Collapsed;
        OpenEditorColumn();
        TypeEditorPanel.Visibility = Visibility.Visible;
    }

    private void ShowItemEditor()
    {
        TypeEditorPanel.Visibility = Visibility.Collapsed;
        OpenEditorColumn();
        ItemEditorPanel.Visibility = Visibility.Visible;
    }

    private void OpenEditorColumn()
    {
        if (EditorColumn.Width.Value <= 1)
        {
            EditorColumn.Width = _lastEditorWidth.Value > 1
                ? _lastEditorWidth
                : new GridLength(280);
        }
    }

    private void OnCollapseEditorClick(object sender, RoutedEventArgs e)
    {
        if (EditorColumn.Width.Value > 1)
        {
            _lastEditorWidth = EditorColumn.Width;
        }

        TypeEditorPanel.Visibility = Visibility.Collapsed;
        ItemEditorPanel.Visibility = Visibility.Collapsed;
        EditorColumn.Width = new GridLength(0);
    }

    /// <summary>用户把分隔条拖到 0 时视为收起，避免编辑栏残留窄条。</summary>
    private void OnEditorPanelSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (EditorColumn.Width.Value > 1 ||
            (TypeEditorPanel.Visibility == Visibility.Collapsed &&
             ItemEditorPanel.Visibility == Visibility.Collapsed))
        {
            return;
        }

        TypeEditorPanel.Visibility = Visibility.Collapsed;
        ItemEditorPanel.Visibility = Visibility.Collapsed;
    }

    private void OnEditorLostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        => EditorAutoSave.OnLostKeyboardFocus(sender, e);

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T target)
            {
                return target;
            }

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }
}
