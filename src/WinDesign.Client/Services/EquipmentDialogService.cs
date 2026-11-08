using System.Windows;
using WinDesign.Client.ViewModels;
using WinDesign.Client.Views;
using WinDesign.Contracts.Equipment;

namespace WinDesign.Client.Services;

public sealed class EquipmentDialogService : IEquipmentDialogService
{
    public EquipmentUpsertRequest? EditEquipment(EquipmentEditorContext context)
    {
        var viewModel = new EquipmentEditorViewModel(context);
        var dialog = new EquipmentPropertyDialog
        {
            DataContext = viewModel,
            Owner = Application.Current?.MainWindow
        };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public bool Confirm(string message) =>
        MessageBox.Show(message, "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
