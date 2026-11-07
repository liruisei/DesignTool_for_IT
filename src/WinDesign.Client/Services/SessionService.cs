using WinDesign.Contracts.Auth;

namespace WinDesign.Client.Services;

public sealed class SessionService
{
    public SessionService(IApiClient? api = null)
    {
        Api = api ?? new ApiClient();
    }

    public IApiClient Api { get; }
    public CurrentUserDto? User { get; private set; }

    public bool IsAdmin => User?.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase) == true;
    public bool IsDesigner => User?.Roles.Contains("Designer", StringComparer.OrdinalIgnoreCase) == true;

    public void SignIn(LoginResponse response)
    {
        User = response.User;
        Api.SetToken(response.AccessToken);
    }

    public void SignOut()
    {
        User = null;
        Api.SetToken(null);
    }
}
