using System.Windows.Controls;

namespace WinDesign.Client.Views;

public partial class DictionaryView : UserControl
{
    public DictionaryView()
    {
        InitializeComponent();
    }

    private void OnEditorLostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        => EditorAutoSave.OnLostKeyboardFocus(sender, e);
}
