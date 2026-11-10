using System.Windows;
using WinDesign.Client.ViewModels;
using WinDesign.Client.Views;

namespace WinDesign.Client.Services;

public sealed class EquipmentSpecDialogService : IEquipmentSpecDialogService
{
    public EquipmentSpecEditResult? EditSpec(EquipmentSpecEditorContext context)
    {
        var viewModel = new EquipmentSpecEditorViewModel(context);
        var dialog = new EquipmentSpecPropertyDialog
        {
            DataContext = viewModel,
            Owner = Application.Current?.MainWindow
        };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public bool Confirm(string message) =>
        MessageBox.Show(message, "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
