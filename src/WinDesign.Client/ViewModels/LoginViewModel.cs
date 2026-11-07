using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;

namespace WinDesign.Client.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly SessionService _session;

    [ObservableProperty]
    private string serverUrl;

    [ObservableProperty]
    private string userName;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    public event EventHandler? LoginSucceeded;

    public LoginViewModel(SessionService session, AppSettings settings)
    {
        _session = session;
        serverUrl = settings.ServerUrl;
        userName = settings.UserName;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ErrorMessage = null;
        if (string.IsNullOrWhiteSpace(ServerUrl))
        {
            ErrorMessage = "请输入服务器地址";
            return;
        }
        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "请输入用户名和密码";
            return;
        }

        IsBusy = true;
        try
        {
            _session.Api.Configure(ServerUrl);
            var response = await _session.Api.LoginAsync(UserName.Trim(), Password);
            _session.SignIn(response);
            SettingsService.Save(new AppSettings(_session.Api.BaseUrl, UserName.Trim()));
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = "无法连接服务器：" + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
