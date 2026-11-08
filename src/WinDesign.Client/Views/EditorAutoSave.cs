using System.Windows;
using System.Windows.Input;
using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;

namespace WinDesign.Client.Views;

/// <summary>
/// 表单编辑区失去焦点（用户点到其他区域）时自动保存已有记录。
/// 新建未保存的记录不会在这里自动创建，必须显式点击“保存”。
/// </summary>
internal static class EditorAutoSave
{
    public static async void OnLostKeyboardFocus(object? sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is not FrameworkElement editor || editor.IsKeyboardFocusWithin)
        {
            return;
        }

        // 焦点仍在编辑区内部（例如从一个输入框切到另一个）时不保存。
        if (e.NewFocus is UIElement newFocus &&
            (ReferenceEquals(editor, newFocus) || editor.IsAncestorOf(newFocus)))
        {
            return;
        }

        if (editor.DataContext is not ViewModelBase viewModel ||
            viewModel.IsBusy ||
            viewModel is not IPendingChanges pending)
        {
            return;
        }

        try
        {
            await pending.SavePendingAsync();
        }
        catch (ApiException)
        {
            // 保存错误已写入 ViewModel.ErrorMessage，由表单区域展示。
        }
        catch (Exception)
        {
            // 同上：避免一次自动保存失败导致界面线程异常。
        }
    }
}
