using System.Windows;
using WinDesign.Client.ViewModels;
using WinDesign.Client.Views;

namespace WinDesign.Client.Services;

public sealed class PbsDialogService : IPbsDialogService
{
    public PbsNodeEditorResult? EditPbsNode(PbsNodeEditorContext context)
    {
        var viewModel = new PbsNodeEditorViewModel(context);
        var dialog = new PbsNodeDialog
        {
            DataContext = viewModel,
            Owner = Application.Current?.MainWindow
        };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public PlantEditorResult? EditPlant(PlantEditorContext context)
    {
        var viewModel = new PlantEditorViewModel(context);
        var dialog = new PlantDialog
        {
            DataContext = viewModel,
            Owner = Application.Current?.MainWindow
        };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }

    public bool Confirm(string message) =>
        MessageBox.Show(message, "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
}
