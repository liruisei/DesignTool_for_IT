using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;

namespace WinDesign.Client.ViewModels;

public interface IRefreshable
{
    Task RefreshAsync();
}

/// <summary>
/// 切换工作区前需要主动提交的表单型模块。索引表 / PBS 等通过弹窗即时保存的模块不需要实现。
/// </summary>
public interface IPendingChanges
{
    /// <summary>
    /// 保存当前表单中尚未提交的编辑。新建未保存的记录不自动创建。
    /// 保存失败时抛出异常，调用方应中止工作区切换。
    /// </summary>
    Task SavePendingAsync();
}

public abstract partial class ViewModelBase : ObservableObject, IRefreshable, IDisposable
{
    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    public virtual Task RefreshAsync() => Task.CompletedTask;

    /// <summary>文档关闭或工作区切换时释放事件订阅。</summary>
    public virtual void Dispose()
    {
    }

    [RelayCommand]
    private Task Refresh() => RefreshAsync();

    protected async Task RunAsync(Func<Task> action)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;
            await action();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
