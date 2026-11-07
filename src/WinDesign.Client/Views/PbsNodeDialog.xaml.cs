using System.Windows;
using WinDesign.Client.ViewModels;

namespace WinDesign.Client.Views;

public partial class PbsNodeDialog : Window
{
    public PbsNodeDialog()
    {
        InitializeComponent();
    }

    public PbsNodeEditorResult? Result { get; private set; }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PbsNodeEditorViewModel viewModel)
        {
            return;
        }

        if (!viewModel.TryBuild(out var result, out var error))
        {
            viewModel.ErrorMessage = error;
            return;
        }

        Result = result;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
