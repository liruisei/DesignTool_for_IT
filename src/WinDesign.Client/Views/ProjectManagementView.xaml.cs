using System.Windows;
using System.Windows.Controls;

namespace WinDesign.Client.Views;

public partial class ProjectManagementView : UserControl
{
    private GridLength _lastEditorWidth = new(380);

    public ProjectManagementView()
    {
        InitializeComponent();
    }

    private void OnToggleEditorClick(object sender, RoutedEventArgs e)
    {
        if (EditorColumn.Width.Value > 1)
        {
            _lastEditorWidth = EditorColumn.Width;
            EditorColumn.Width = new GridLength(0);
        }
        else
        {
            EditorColumn.Width = _lastEditorWidth.Value > 1
                ? _lastEditorWidth
                : new GridLength(380);
        }

        UpdateToggleText();
    }

    private void OnEditorPanelSizeChanged(object sender, SizeChangedEventArgs e)
    {
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
