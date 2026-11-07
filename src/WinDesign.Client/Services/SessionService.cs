using WinDesign.Contracts.Auth;

namespace WinDesign.Client.Services;

public sealed class SessionService
{
    public ApiClient Api { get; } = new();
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
