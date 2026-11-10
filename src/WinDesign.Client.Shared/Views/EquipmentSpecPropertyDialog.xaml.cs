using System.Windows;
using System.Windows.Controls;
using WinDesign.Client.ViewModels;

namespace WinDesign.Client.Views;

public partial class EquipmentSpecPropertyDialog : Window
{
    public EquipmentSpecPropertyDialog()
    {
        InitializeComponent();
    }

    public EquipmentSpecEditResult? Result { get; private set; }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (DataContext is not EquipmentSpecEditorViewModel viewModel)
        {
            return;
        }

        // 单元格输入必须显式提交到绑定源，否则最后一格未失焦时不会进入 TryBuild。
        ParameterGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ParameterGrid.CommitEdit(DataGridEditingUnit.Row, true);

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
