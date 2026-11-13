using System.Windows;
using WinDesign.Client.ViewModels;
using WinDesign.Client.Views;

namespace WinDesign.Client.Services;

public sealed class CategoryDialogService : ICategoryDialogService
{
    public CategoryEditResult? EditCategory(CategoryEditorContext context)
    {
        var viewModel = new CategoryEditorViewModel(context);
        var dialog = new CategoryPropertyDialog
        {
            DataContext = viewModel,
            Owner = Application.Current?.MainWindow
        };
        return dialog.ShowDialog() == true ? dialog.Result : null;
    }
}
