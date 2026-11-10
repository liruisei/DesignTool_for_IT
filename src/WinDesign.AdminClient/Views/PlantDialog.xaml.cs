using System.Windows;
using WinDesign.AdminClient.ViewModels;

namespace WinDesign.AdminClient.Views;

public partial class PlantDialog : Window
{
    public PlantDialog()
    {
        InitializeComponent();
    }

    public PlantEditorResult? Result { get; private set; }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PlantEditorViewModel viewModel)
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
