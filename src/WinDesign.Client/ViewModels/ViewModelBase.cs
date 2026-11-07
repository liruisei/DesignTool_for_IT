using CommunityToolkit.Mvvm.ComponentModel;
using WinDesign.Client.Services;

namespace WinDesign.Client.ViewModels;

public interface IRefreshable
{
    Task RefreshAsync();
}

public abstract partial class ViewModelBase : ObservableObject, IRefreshable
{
    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    public virtual Task RefreshAsync() => Task.CompletedTask;

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
