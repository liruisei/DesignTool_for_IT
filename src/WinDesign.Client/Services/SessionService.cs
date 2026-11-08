using WinDesign.Contracts.Auth;
using WinDesign.Contracts.Workspaces;

namespace WinDesign.Client.Services;

public sealed class SessionService
{
    public SessionService(IApiClient? api = null)
    {
        Api = api ?? new ApiClient();
    }

    public IApiClient Api { get; }
    public CurrentUserDto? User { get; private set; }

    /// <summary>登录工作区：工厂（PBS 根节点）。</summary>
    public WorkspacePlantDto? Plant { get; private set; }

    /// <summary>登录工作区：项目（设计与报表范围）。</summary>
    public WorkspaceProjectDto? Project { get; private set; }

    public Guid? PlantId => Plant?.Id;
    public Guid? ProjectId => Project?.Id;

    public bool IsAdmin => User?.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase) == true;
    public bool IsDesigner => User?.Roles.Contains("Designer", StringComparer.OrdinalIgnoreCase) == true;
    public bool HasWorkspace => Plant is not null && Project is not null;

    /// <summary>管理员未选择工厂/项目，仅使用系统配置功能。</summary>
    public bool IsManagementMode { get; private set; }

    /// <summary>登录成功后只保存身份令牌，尚未选择工作区。</summary>
    public void SignIn(LoginResponse response)
    {
        User = response.User;
        Plant = null;
        Project = null;
        IsManagementMode = false;
        Api.SetToken(response.AccessToken);
    }

    /// <summary>选择工厂 + 项目后替换为带 plantId / projectId 声明的令牌。</summary>
    public void SetWorkspace(WorkspaceResponse response)
    {
        User = response.User;
        Plant = response.Plant;
        Project = response.Project;
        IsManagementMode = false;
        Api.SetToken(response.AccessToken);
    }

    /// <summary>管理员进入管理模块：不绑定工厂/项目，只有系统配置功能。</summary>
    public void SetManagementMode(ManagementSessionResponse response)
    {
        User = response.User;
        Plant = null;
        Project = null;
        IsManagementMode = true;
        Api.SetToken(response.AccessToken);
    }

    public void SignOut()
    {
        User = null;
        Plant = null;
        Project = null;
        IsManagementMode = false;
        Api.SetToken(null);
    }
}
