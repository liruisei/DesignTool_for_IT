using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Contracts.Projects;

namespace WinDesign.Client.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SessionService _session;
    private readonly ProjectContext _projectContext = new();

    [ObservableProperty]
    private NavigationItem? selectedNavigationItem;

    [ObservableProperty]
    private ViewModelBase? currentViewModel;

    [ObservableProperty]
    private ProjectDto? selectedProject;

    [ObservableProperty]
    private string statusText = "就绪";

    public ObservableCollection<NavigationItem> NavigationItems { get; } = new();
    public ObservableCollection<ProjectDto> Projects { get; } = new();

    public string UserDisplayName => _session.User?.DisplayName ?? string.Empty;
    public string UserName => _session.User?.UserName ?? string.Empty;
    public string RoleText => string.Join(" / ", _session.User?.Roles ?? new List<string>());
    public string ServerUrl => _session.Api.BaseUrl;
    public bool IsAdmin => _session.IsAdmin;

    public event EventHandler? LogoutRequested;

    public MainViewModel(SessionService session)
    {
        _session = session;
        BuildNavigation();
        SelectedNavigationItem = NavigationItems.FirstOrDefault();
        _ = LoadProjectsAsync();
    }

    private void BuildNavigation()
    {
        NavigationItems.Add(new NavigationItem
        {
            Key = "dashboard",
            Title = "工作台",
            Subtitle = "项目概览与快捷入口",
            Glyph = "◈",
            Factory = () => new DashboardViewModel(_session)
        });

        NavigationItems.Add(new NavigationItem
        {
            Key = "projects",
            Title = "项目管理",
            Subtitle = "项目登记、阶段与成员",
            Glyph = "▦",
            Factory = () => new ProjectManagementViewModel(_session)
        });

        NavigationItems.Add(new NavigationItem
        {
            Key = "pbs",
            Title = "工厂 PBS",
            Subtitle = "工厂—区域—装置—系统分解结构",
            Glyph = "◇",
            Factory = () => new PbsViewModel(_session, _projectContext)
        });

        NavigationItems.Add(new NavigationItem
        {
            Key = "dictionaries",
            Title = "编码定义",
            Subtitle = "专业、设备类型、材料分类、状态字典",
            Glyph = "☰",
            Factory = () => new DictionaryViewModel(_session)
        });

        NavigationItems.Add(new NavigationItem
        {
            Key = "equipment",
            Title = "设备索引",
            Subtitle = "位号、设备类型、PBS 与设计状态",
            Glyph = "▤",
            Factory = () => new EquipmentIndexViewModel(_session, _projectContext)
        });

        NavigationItems.Add(new NavigationItem
        {
            Key = "specs",
            Title = "设备规格",
            Subtitle = "规格书模板、参数与版本",
            Glyph = "✎",
            Factory = () => new ModulePlaceholderViewModel(
                "设备规格设计",
                "下一阶段",
                "为每类设备定义规格书模板，按设备填写参数，形成可版本化、可输出的规格书数据。",
                new[]
                {
                    "按设备类型定义规格字段",
                    "规格书版本与审批状态",
                    "参数校验与缺项检查",
                    "导出 Word / Excel / PDF"
                })
        });

        NavigationItems.Add(new NavigationItem
        {
            Key = "materials",
            Title = "材料附件",
            Subtitle = "材料清单、附件与安装件",
            Glyph = "▣",
            Factory = () => new ModulePlaceholderViewModel(
                "材料附件设计",
                "下一阶段",
                "维护设备配套的材料、安装附件、电缆与穿管等，按项目 / PBS / 设备汇总，形成材料表。",
                new[]
                {
                    "材料编码与材料字典联动",
                    "设备—材料—附件关联",
                    "按 PBS / 专业 / 图号汇总",
                    "材料表输出与采购清单"
                })
        });

        NavigationItems.Add(new NavigationItem
        {
            Key = "reports",
            Title = "报表输出",
            Subtitle = "索引表、规格书、材料表与电缆表",
            Glyph = "▥",
            Factory = () => new ModulePlaceholderViewModel(
                "报表输出",
                "下一阶段",
                "把索引、规格、材料数据按模板输出为 Excel、Word、PDF，并预留 AutoCAD 出图接口。",
                new[]
                {
                    "报表模板管理",
                    "按项目 / 专业 / 图号输出",
                    "Excel / Word / PDF",
                    "AutoCAD 表格与图纸输出（预留）"
                })
        });

        if (_session.IsAdmin)
        {
            NavigationItems.Add(new NavigationItem
            {
                Key = "users",
                Title = "用户管理",
                Subtitle = "账号、角色与启停",
                Glyph = "●",
                AdminOnly = true,
                Factory = () => new UserManagementViewModel(_session)
            });
        }
    }

    partial void OnSelectedNavigationItemChanged(NavigationItem? value)
    {
        if (value is null)
        {
            return;
        }

        CurrentViewModel = value.Factory();
        StatusText = value.Subtitle;
    }

    partial void OnSelectedProjectChanged(ProjectDto? value)
    {
        _projectContext.CurrentProject = value;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (CurrentViewModel is not null)
        {
            await CurrentViewModel.RefreshAsync();
        }
        await LoadProjectsAsync();
        StatusText = "已刷新 " + DateTime.Now.ToString("HH:mm:ss");
    }

    [RelayCommand]
    private void Logout()
    {
        _session.SignOut();
        LogoutRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task LoadProjectsAsync()
    {
        try
        {
            var result = await _session.Api.GetProjectsAsync();
            Projects.Clear();
            foreach (var project in result.Items)
            {
                Projects.Add(project);
            }
            SelectedProject ??= Projects.FirstOrDefault();
        }
        catch
        {
            // project selector is non-critical for the shell
        }
    }
}
