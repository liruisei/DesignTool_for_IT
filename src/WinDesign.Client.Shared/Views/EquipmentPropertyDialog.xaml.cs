using System.Windows;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Equipment;

namespace WinDesign.Client.Views;

public partial class EquipmentPropertyDialog : Window
{
    public EquipmentPropertyDialog()
    {
        InitializeComponent();
    }

    public EquipmentUpsertRequest? Result { get; private set; }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (DataContext is not EquipmentEditorViewModel viewModel)
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
