using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Auth;

namespace WinDesign.Client.Tests;

public class LoginViewModelTests
{
    [Fact]
    public async Task Login_Success_SetsSessionAndRaisesEvent()
    {
        var api = new FakeApiClient
        {
            LoginResult = new LoginResponse(
                "token-123",
                DateTime.UtcNow.AddHours(1),
                new CurrentUserDto(Guid.NewGuid(), "admin", "管理员",
                    new[] { "Admin" }, new[] { "users.read" }))
        };
        var session = new SessionService(api);
        var viewModel = new LoginViewModel(session, new AppSettings("http://test", "admin"))
        {
            UserName = "admin",
            Password = "admin123"
        };
        var raised = false;
        viewModel.LoginSucceeded += (_, _) => raised = true;

        await viewModel.LoginCommand.ExecuteAsync(null);

        Assert.True(raised);
        Assert.NotNull(session.User);
        Assert.Equal("admin", session.User!.UserName);
        Assert.Equal("token-123", api.Token);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task Login_Failure_SetsErrorMessageAndDoesNotRaiseEvent()
    {
        var api = new FakeApiClient
        {
            LoginError = new ApiException("用户名或密码错误")
        };
        var session = new SessionService(api);
        var viewModel = new LoginViewModel(session, new AppSettings("http://test", "admin"))
        {
            UserName = "admin",
            Password = "bad"
        };
        var raised = false;
        viewModel.LoginSucceeded += (_, _) => raised = true;

        await viewModel.LoginCommand.ExecuteAsync(null);

        Assert.False(raised);
        Assert.Null(session.User);
        Assert.Equal("用户名或密码错误", viewModel.ErrorMessage);
    }
}
