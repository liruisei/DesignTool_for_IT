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

    public ObservableCollection<ExplorerNode> ExplorerNodes { get; } = new();
    public ObservableCollection<DocumentTab> Documents { get; } = new();

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
    public string PlantName => _session.Plant?.Name ?? "未选择工厂";
    public string ProjectName => _session.Project?.Name ?? "未选择项目";
    public string WorkspaceText => PlantName + " / " + ProjectName;
    public string CurrentProjectName => _session.Project?.Name ?? "未选择项目";

    public event EventHandler? LogoutRequested;
    public event EventHandler? SwitchWorkspaceRequested;

    /// <summary>供窗口层打开工作区选择窗口。</summary>
    public SessionService Session => _session;

    public MainViewModel(SessionService session)
    {
        _session = session;
        _projectContext.CurrentProject = ToProjectDto(session);
        BuildExplorer();
        OpenIndex();
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
            Key = "project-config",
            Title = "项目配置",
            Glyph = "▤",
            Open = OpenProjectConfig
        });
        system.Children.Add(new ExplorerNode
        {
            Key = "pbs",
            Title = "PBS管理",
            Glyph = "◇",
            Open = OpenPbs
        });
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
    private void OpenProjectConfig() =>
        OpenDocument("project-config", "项目配置", () => new ProjectManagementViewModel(_session));

    [RelayCommand]
    private void OpenPbs() =>
        OpenDocument("pbs", "PBS管理", () => new PbsViewModel(_session, _projectContext));

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
        tab.ViewModel.Dispose();
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
        StatusText = "已刷新 " + DateTime.Now.ToString("HH:mm:ss");
    }

    [RelayCommand]
    private void SwitchWorkspace() =>
        SwitchWorkspaceRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// 切换工作区前自动保存所有打开文档中的待提交编辑。
    /// 返回第一个错误信息；null 表示全部保存成功。
    /// </summary>
    public async Task<string?> SavePendingDocumentsAsync()
    {
        foreach (var document in Documents)
        {
            if (document.ViewModel is not IPendingChanges pending)
            {
                continue;
            }

            try
            {
                await pending.SavePendingAsync();
            }
            catch (ApiException ex)
            {
                return document.Title + "：" + ex.Message;
            }
            catch (Exception ex)
            {
                return document.Title + "：" + ex.Message;
            }
        }

        return null;
    }

    /// <summary>
    /// 工作区选择窗口已经换发新令牌后调用：重建项目上下文并重开默认文档，
    /// 避免旧工厂/旧项目的文档与缓存继续显示。
    /// </summary>
    public void ApplyWorkspaceChange()
    {
        SelectedDocument = null;
        foreach (var document in Documents)
        {
            document.ViewModel.Dispose();
        }
        Documents.Clear();

        _projectContext.CurrentProject = ToProjectDto(_session);
        OnPropertyChanged(nameof(PlantName));
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(WorkspaceText));
        OnPropertyChanged(nameof(CurrentProjectName));

        OpenIndex();
        StatusText = "已切换工作区：" + WorkspaceText;
    }

    [RelayCommand]
    private void Logout()
    {
        _session.SignOut();
        LogoutRequested?.Invoke(this, EventArgs.Empty);
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

    private static ProjectDto? ToProjectDto(SessionService session)
    {
        if (session.Project is not { } project)
        {
            return null;
        }

        return new ProjectDto
        {
            Id = project.Id,
            PlantId = session.Plant?.Id ?? Guid.Empty,
            PlantName = session.Plant?.Name,
            Code = project.Code,
            Name = project.Name,
            Phase = project.Phase,
            Status = project.Status,
            CreatedAt = DateTime.UtcNow
        };
    }
}
