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

    /// <summary>
    /// 可选的保存回调：返回 null 表示保存成功并关闭弹窗；
    /// 返回错误信息（如位号重复）时保持弹窗打开并在弹窗内提示。
    /// </summary>
    public Func<EquipmentUpsertRequest, Task<string?>>? SaveAsync { get; init; }

    private async void OnOk(object sender, RoutedEventArgs e)
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

        if (SaveAsync is not null)
        {
            OkButton.IsEnabled = false;
            try
            {
                var saveError = await SaveAsync(result);
                if (!string.IsNullOrWhiteSpace(saveError))
                {
                    viewModel.ErrorMessage = saveError;
                    return;
                }
            }
            catch (Exception ex)
            {
                viewModel.ErrorMessage = ex.Message;
                return;
            }
            finally
            {
                OkButton.IsEnabled = true;
            }
        }

        Result = result;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
