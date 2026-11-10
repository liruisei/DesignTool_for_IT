using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;

namespace WinDesign.AdminClient.ViewModels;

/// <summary>
/// 管理客户端外壳：只承载系统配置类文档，不选择工厂/项目。
/// 与设计客户端的 MainViewModel 完全独立，各自维护自己的导航树。
/// </summary>
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
    public string WorkspaceText => "系统配置（不绑定工厂/项目）";

    public event EventHandler? LogoutRequested;

    public SessionService Session => _session;

    public MainViewModel(SessionService session)
    {
        _session = session;
        BuildExplorer();
        OpenPbs();
    }

    private void BuildExplorer()
    {
        ExplorerNodes.Clear();
        SelectedExplorerNode = null;

        var system = new ExplorerNode
        {
            Key = "system",
            Title = "系统",
            Glyph = "⚙",
            IsFolder = true
        };
        system.Children.Add(new ExplorerNode
        {
            Key = "pbs",
            Title = "PBS管理",
            Glyph = "◇",
            Open = OpenPbs
        });
        system.Children.Add(new ExplorerNode
        {
            Key = "project-config",
            Title = "项目配置",
            Glyph = "▤",
            Open = OpenProjectConfig
        });
        system.Children.Add(new ExplorerNode
        {
            Key = "dictionaries",
            Title = "编码管理",
            Glyph = "☰",
            Open = OpenDictionaries
        });
        system.Children.Add(new ExplorerNode
        {
            Key = "users",
            Title = "用户管理",
            Glyph = "●",
            Open = OpenUsers
        });
        ExplorerNodes.Add(system);
    }

    [RelayCommand]
    private void OpenProjectConfig() =>
        OpenDocument("project-config", "项目配置", () => new ProjectManagementViewModel(_session));

    [RelayCommand]
    private void OpenPbs() =>
        // PBS 管理是管理模块的默认常驻页面，不允许关闭。
        OpenDocument("pbs", "PBS管理", () => new PbsViewModel(_session, _projectContext), closable: false);

    [RelayCommand]
    private void OpenDictionaries() =>
        OpenDocument("dictionaries", "编码管理", () => new DictionaryViewModel(_session));

    [RelayCommand]
    private void OpenUsers() =>
        OpenDocument("users", "用户管理", () => new UserManagementViewModel(_session));

    public void OpenExplorerNode(ExplorerNode? node) => node?.Open?.Invoke();

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

    /// <summary>
    /// 管理客户端没有工作区切换；保留该方法以便将来批量保存或退出前检查。
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
            catch (Exception ex)
            {
                return document.Title + "：" + ex.Message;
            }
        }

        return null;
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
}
