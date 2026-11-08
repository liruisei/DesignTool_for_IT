using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Contracts.Workspaces;

namespace WinDesign.Client.ViewModels;

/// <summary>
/// 登录后的工作区选择：先选工厂（PBS 根节点），再选项目。
/// 选择结果会换取一个带 plantId / projectId 声明的新令牌。
/// </summary>
public partial class WorkspaceSelectionViewModel : ObservableObject
{
    private readonly SessionService _session;
    private readonly Guid? _initialPlantId;
    private readonly Guid? _initialProjectId;
    private readonly bool _isSwitching;

    public ObservableCollection<WorkspacePlantDto> Plants { get; } = new();
    public ObservableCollection<WorkspaceProjectDto> Projects { get; } = new();

    [ObservableProperty]
    private WorkspacePlantDto? selectedPlant;

    [ObservableProperty]
    private WorkspaceProjectDto? selectedProject;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isLoading = true;

    public string UserDisplayName => _session.User?.DisplayName ?? string.Empty;

    /// <summary>登录后的首次选择，还是主界面内的切换。</summary>
    public bool IsSwitching => _isSwitching;
    public bool CanUseManagementMode => _session.IsAdmin;
    public string WindowTitle => IsSwitching ? "切换工作区" : "选择工作区";
    public string HeadingText => IsSwitching ? "切换工作区" : "选择工作区";
    public string HintText => IsSwitching
        ? "选择要切换到的工厂与项目，双击项目或点击「进入工作区」。管理员也可进入管理模块。切换后当前打开的设计文档会被关闭，并按新工作区重新加载。"
        : "先选择工厂（PBS 根节点），再选择项目。双击项目或点击「进入工作区」继续；管理员也可以不选工厂/项目，直接进入管理模块。";

    public event EventHandler? SelectionSucceeded;

    public WorkspaceSelectionViewModel(
        SessionService session,
        Guid? initialPlantId = null,
        Guid? initialProjectId = null,
        bool isSwitching = false)
    {
        _session = session;
        _initialPlantId = initialPlantId;
        _initialProjectId = initialProjectId;
        _isSwitching = isSwitching || initialPlantId is not null;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var workspaces = await _session.Api.GetWorkspacesAsync();
            Plants.Clear();
            foreach (var plant in workspaces)
            {
                Plants.Add(plant);
            }

            SelectedPlant = Plants.FirstOrDefault(x => x.Id == _initialPlantId)
                            ?? Plants.FirstOrDefault();

            // 切换工作区时尽量停留/预选当前项目；若项目不在该工厂下则回退到第一个。
            if (SelectedPlant is not null && _initialProjectId is Guid projectId)
            {
                SelectedProject = SelectedPlant.Projects.FirstOrDefault(x => x.Id == projectId)
                                  ?? SelectedProject;
            }

            if (Plants.Count == 0)
            {
                ErrorMessage = "当前账号没有任何工厂/项目权限，请联系管理员分配项目成员身份。";
            }
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = "无法加载可访问的工厂/项目：" + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedPlantChanged(WorkspacePlantDto? value)
    {
        Projects.Clear();
        if (value is not null)
        {
            foreach (var project in value.Projects)
            {
                Projects.Add(project);
            }
        }
        SelectedProject = Projects.FirstOrDefault();
    }

    /// <summary>
    /// 管理员进入管理模块：不选择工厂/项目，只开放系统配置功能。
    /// </summary>
    [RelayCommand]
    private async Task EnterManagementAsync()
    {
        if (!CanUseManagementMode || IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _session.Api.EnterManagementModeAsync();
            _session.SetManagementMode(response);
            SelectionSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = "无法进入管理模块：" + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (IsBusy)
        {
            return;
        }
        if (SelectedPlant is null || SelectedProject is null)
        {
            ErrorMessage = "请选择工厂和项目";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var response = await _session.Api.SelectWorkspaceAsync(
                SelectedPlant.Id,
                SelectedProject.Id);
            _session.SetWorkspace(response);
            SelectionSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = "无法进入工作区：" + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
