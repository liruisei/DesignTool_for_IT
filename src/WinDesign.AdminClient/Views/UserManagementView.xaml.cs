using System.Windows;
using System.Windows.Controls;

namespace WinDesign.AdminClient.Views;

public partial class UserManagementView : UserControl
{
    private GridLength _lastEditorWidth = new(370);

    public UserManagementView()
    {
        InitializeComponent();
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

    private void OnNewUserClick(object sender, RoutedEventArgs e) => ExpandEditor();

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
                : new GridLength(370);
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
