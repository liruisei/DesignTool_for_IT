using System.Windows;
using WinDesign.Client.ViewModels;

namespace WinDesign.Client.Views;

public partial class CategoryPropertyDialog : Window
{
    public CategoryPropertyDialog()
    {
        InitializeComponent();
    }

    public CategoryEditResult? Result { get; private set; }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CategoryEditorViewModel viewModel)
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
