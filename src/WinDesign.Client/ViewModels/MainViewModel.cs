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
    private bool _startupDocumentOpened;

    public ObservableCollection<ProjectDto> Projects { get; } = new();
    public ObservableCollection<ExplorerNode> ExplorerNodes { get; } = new();
    public ObservableCollection<DocumentTab> Documents { get; } = new();

    [ObservableProperty]
    private ProjectDto? selectedProject;

    [ObservableProperty]
    private ExplorerNode? selectedExplorerNode;

    [ObservableProperty]
    private DocumentTab? selectedDocument;

    [ObservableProperty]
    private string statusText = "就绪";

    public string UserDisplayName => _session.User?.DisplayName ?? string.Empty;
    public string UserName => _session.User?.UserName ?? string.Empty;
    public string RoleText => string.Join(" / ", _session.User?.Roles ?? new List<string>());
    public string ServerUrl => _session.Api.BaseUrl;
    public bool IsAdmin => _session.IsAdmin;
    public string CurrentProjectName => SelectedProject?.Name ?? "未选择项目";

    public event EventHandler? LogoutRequested;

    public MainViewModel(SessionService session)
    {
        _session = session;
        BuildExplorer();
        _ = LoadProjectsAsync();
    }

    private void BuildExplorer()
    {
        var project = new ExplorerNode
        {
            Key = "project",
            Title = "项目",
            Glyph = "▣",
            IsFolder = true
        };
        project.Children.Add(new ExplorerNode
        {
            Key = "project-info",
            Title = "项目信息",
            Glyph = "▤",
            Open = OpenProjectInfo
        });
        project.Children.Add(new ExplorerNode
        {
            Key = "pbs",
            Title = "工厂 PBS",
            Glyph = "◇",
            Open = OpenPbs
        });
        project.Children.Add(new ExplorerNode
        {
            Key = "index",
            Title = "索引表",
            Glyph = "▦",
            Open = OpenIndex
        });
        project.Children.Add(new ExplorerNode
        {
            Key = "specs",
            Title = "设备规格",
            Glyph = "✎",
            Open = OpenSpecs
        });
        project.Children.Add(new ExplorerNode
        {
            Key = "materials",
            Title = "材料附件",
            Glyph = "▣",
            Open = OpenMaterials
        });
        project.Children.Add(new ExplorerNode
        {
            Key = "reports",
            Title = "报表输出",
            Glyph = "▥",
            Open = OpenReports
        });
        ExplorerNodes.Add(project);

        var system = new ExplorerNode
        {
            Key = "system",
            Title = "系统",
            Glyph = "⚙",
            IsFolder = true
        };
        system.Children.Add(new ExplorerNode
        {
            Key = "dictionaries",
            Title = "编码定义",
            Glyph = "☰",
            Open = OpenDictionaries
        });
        if (_session.IsAdmin)
        {
            system.Children.Add(new ExplorerNode
            {
                Key = "users",
                Title = "用户管理",
                Glyph = "●",
                Open = OpenUsers
            });
        }
        ExplorerNodes.Add(system);
    }

    [RelayCommand]
    private void OpenProjectInfo() =>
        OpenDocument("project-info", "项目信息", () => new ProjectManagementViewModel(_session), closable: false);

    [RelayCommand]
    private void OpenPbs() =>
        OpenDocument("pbs", "工厂 PBS", () => new PbsViewModel(_session, _projectContext));

    [RelayCommand]
    private void OpenIndex() =>
        OpenDocument("index", "索引表", () => new EquipmentIndexViewModel(_session, _projectContext));

    [RelayCommand]
    private void OpenDictionaries() =>
        OpenDocument("dictionaries", "编码定义", () => new DictionaryViewModel(_session));

    [RelayCommand]
    private void OpenUsers() =>
        OpenDocument("users", "用户管理", () => new UserManagementViewModel(_session));

    [RelayCommand]
    private void OpenSpecs() =>
        OpenDocument("specs", "设备规格", () => new ModulePlaceholderViewModel(
            "设备规格设计",
            "第二阶段",
            "按设备类型定义规格字段模板、版本与审批状态，校验必填参数，并输出规格书。",
            new[]
            {
                "按设备类型定义规格字段",
                "规格书版本与审批状态",
                "参数校验与缺项检查",
                "导出 Word / Excel / PDF"
            }));

    [RelayCommand]
    private void OpenMaterials() =>
        OpenDocument("materials", "材料附件", () => new ModulePlaceholderViewModel(
            "材料附件设计",
            "第二阶段",
            "维护设备配套材料、安装附件、电缆与穿管，按项目 / PBS / 设备汇总材料表。",
            new[]
            {
                "材料编码与材料字典联动",
                "设备—材料—附件关联",
                "按 PBS / 专业 / 图号汇总",
                "材料表输出与采购清单"
            }));

    [RelayCommand]
    private void OpenReports() =>
        OpenDocument("reports", "报表输出", () => new ModulePlaceholderViewModel(
            "报表输出",
            "第二阶段",
            "将索引、规格、材料数据按模板输出为 Excel、Word、PDF，并预留 AutoCAD 出图接口。",
            new[]
            {
                "报表模板管理",
                "按项目 / 专业 / 图号输出",
                "Excel / Word / PDF",
                "AutoCAD 表格与图纸输出（预留）"
            }));

    public void OpenExplorerNode(ExplorerNode? node)
    {
        node?.Open?.Invoke();
    }

    [RelayCommand]
    private void OpenSelectedExplorerNode() => OpenExplorerNode(SelectedExplorerNode);

    [RelayCommand]
    private void CloseDocument(DocumentTab? tab)
    {
        if (tab is null || !tab.IsClosable)
        {
            return;
        }

        var index = Documents.IndexOf(tab);
        Documents.Remove(tab);
        if (ReferenceEquals(SelectedDocument, tab))
        {
            SelectedDocument = Documents.ElementAtOrDefault(Math.Max(0, index - 1))
                               ?? Documents.LastOrDefault();
        }
    }

    [RelayCommand]
    private void CloseSelectedDocument() => CloseDocument(SelectedDocument);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (SelectedDocument is not null)
        {
            await SelectedDocument.ViewModel.RefreshAsync();
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

    partial void OnSelectedProjectChanged(ProjectDto? value)
    {
        _projectContext.CurrentProject = value;
        OnPropertyChanged(nameof(CurrentProjectName));
    }

    partial void OnSelectedDocumentChanged(DocumentTab? value)
    {
        if (value is null)
        {
            StatusText = "就绪";
            return;
        }

        StatusText = "当前文档：" + value.Title;
        if (value.HasBeenActivated)
        {
            _ = RefreshDocumentAsync(value);
        }
        else
        {
            value.HasBeenActivated = true;
        }
    }

    private void OpenDocument(string key, string title, Func<ViewModelBase> factory, bool closable = true)
    {
        var existing = Documents.FirstOrDefault(x => x.Key == key);
        if (existing is not null)
        {
            var alreadySelected = ReferenceEquals(SelectedDocument, existing);
            SelectedDocument = existing;
            if (alreadySelected)
            {
                _ = RefreshDocumentAsync(existing);
            }
            return;
        }

        var tab = new DocumentTab
        {
            Key = key,
            Title = title,
            ViewModel = factory(),
            IsClosable = closable
        };
        Documents.Add(tab);
        SelectedDocument = tab;
    }

    private static async Task RefreshDocumentAsync(DocumentTab tab)
    {
        try
        {
            await tab.ViewModel.RefreshAsync();
        }
        catch
        {
            // individual modules surface their own error messages
        }
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
            if (!_startupDocumentOpened)
            {
                _startupDocumentOpened = true;
                OpenProjectInfo();
            }
        }
        catch
        {
            // the shell remains usable even if the project list cannot be loaded
        }
    }
}
