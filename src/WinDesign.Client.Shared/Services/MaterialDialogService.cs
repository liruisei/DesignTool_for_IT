using System.Windows;
using WinDesign.Client.ViewModels;
using WinDesign.Client.Views;

namespace WinDesign.Client.Services;

public sealed class MaterialDialogService : IMaterialDialogService
{
    public MaterialEditResult? EditMaterial(MaterialEditorContext context)
    {
        var viewModel = new MaterialEditorViewModel(context);
        var dialog = new MaterialPropertyDialog
        {
            DataContext = viewModel,
            Owner = Application.Current?.MainWindow
        };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public bool Confirm(string message) =>
        MessageBox.Show(message, "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
