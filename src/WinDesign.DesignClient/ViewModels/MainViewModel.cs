using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Projects;

namespace WinDesign.DesignClient.ViewModels;

/// <summary>
/// 设计客户端外壳：在选定「工厂 → 项目」后，承载索引表、规格书、材料表与报表。
/// 系统配置（项目配置 / PBS 管理 / 编码定义 / 用户管理）不在本客户端出现。
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly SessionService _session;
    private readonly IEquipmentDialogService _equipmentDialogs;
    private readonly ICategoryDialogService _categoryDialogs;
    private readonly ProjectContext _projectContext = new();
    private ExplorerNode? _pbsExplorerNode;
    private IReadOnlyList<PbsNodeDto> _pbsNodes = Array.Empty<PbsNodeDto>();

    /// <summary>左侧 PBS 树的异步加载任务，便于测试等待首次加载完成。</summary>
    public Task PbsTreeLoadTask { get; private set; } = Task.CompletedTask;

    /// <summary>上方项目面板：索引表 / 规格书 / 材料表 / 报表输出。</summary>
    public ObservableCollection<ExplorerNode> ProjectExplorerNodes { get; } = new();

    /// <summary>下方 PBS 面板：当前项目 PBS 根节点及其子树，只读。</summary>
    public ObservableCollection<ExplorerNode> PbsExplorerNodes { get; } = new();

    public ObservableCollection<DocumentTab> Documents { get; } = new();

    [ObservableProperty]
    private ExplorerNode? selectedProjectNode;

    [ObservableProperty]
    private ExplorerNode? selectedPbsNode;

    [ObservableProperty]
    private DocumentTab? selectedDocument;

    [ObservableProperty]
    private string statusText = "就绪";

    public string UserDisplayName => _session.User?.DisplayName ?? string.Empty;
    public string UserName => _session.User?.UserName ?? string.Empty;
    public string RoleText => string.Join(" / ", _session.User?.Roles ?? new List<string>());
    public string ServerUrl => _session.Api.BaseUrl;
    public string PlantName => _session.Plant?.Name ?? "未选择工厂";
    public string ProjectName => _session.Project?.Name ?? "未选择项目";
    public string WorkspaceText => PlantName + " / " + ProjectName;

    public event EventHandler? LogoutRequested;
    public event EventHandler? SwitchWorkspaceRequested;

    public SessionService Session => _session;

    public MainViewModel(
        SessionService session,
        IEquipmentDialogService? equipmentDialogs = null,
        ICategoryDialogService? categoryDialogs = null)
    {
        _session = session;
        _equipmentDialogs = equipmentDialogs ?? new EquipmentDialogService();
        _categoryDialogs = categoryDialogs ?? new CategoryDialogService();
        _projectContext.CurrentProject = ToProjectDto(session);
        BuildExplorer();
        PbsTreeLoadTask = LoadPbsTreeAsync();
        OpenIndex();
    }

    private void BuildExplorer()
    {
        ProjectExplorerNodes.Clear();
        PbsExplorerNodes.Clear();
        SelectedProjectNode = null;
        SelectedPbsNode = null;

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
            Title = "规格书",
            Glyph = "✎",
            Open = OpenSpecs
        });
        project.Children.Add(new ExplorerNode
        {
            Key = "materials",
            Title = "材料表",
            Glyph = "▣",
            Open = OpenMaterials
        });
        project.Children.Add(new ExplorerNode
        {
            Key = "telecom",
            Title = "电信统计",
            Glyph = "◈",
            Open = OpenTelecom
        });
        project.Children.Add(new ExplorerNode
        {
            Key = "reports",
            Title = "报表输出",
            Glyph = "▥",
            Open = OpenReports
        });
        ProjectExplorerNodes.Add(project);

        // PBS 独立成下方只读面板，展示当前项目的 PBS 根节点及其子节点。
        // 根节点用工厂编码；工厂、装置、建筑物在树中只显示编号，名称不展开。
        _pbsExplorerNode = new ExplorerNode
        {
            Key = "pbs",
            Title = _session.Plant?.Code ?? "PBS",
            Glyph = "⌂",
            IsFolder = true
        };
        PbsExplorerNodes.Add(_pbsExplorerNode);
    }

    /// <summary>
    /// 加载当前项目所属工厂的 PBS 树，只保留项目根节点及其子树。
    /// 树的维护仍在管理端，设计端只读。
    /// </summary>
    public async Task LoadPbsTreeAsync()
    {
        var pbsNode = _pbsExplorerNode;
        if (pbsNode is null)
        {
            return;
        }

        pbsNode.Children.Clear();

        if (_session.PlantId is not Guid plantId)
        {
            AddPbsPlaceholder(pbsNode, "未选择工厂");
            return;
        }

        try
        {
            var nodes = await _session.Api.GetPbsNodesAsync(plantId);
            _pbsNodes = nodes;

            var equipmentByPbsNode = await LoadEquipmentsByPbsNodeAsync();

            if (_session.Project?.PbsRootNodeId is not Guid rootId)
            {
                ApplyMissingPbsRoot(pbsNode, nodes, equipmentByPbsNode);
                return;
            }

            var root = nodes.FirstOrDefault(x => x.Id == rootId);
            if (root is null)
            {
                AddPbsPlaceholder(pbsNode, "项目 PBS 根节点不存在或已被删除");
                return;
            }

            var childrenByParent = nodes
                .Where(x => x.ParentId is not null)
                .GroupBy(x => x.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            pbsNode.Children.Add(BuildPbsExplorerItem(
                root, childrenByParent, equipmentByPbsNode, new HashSet<Guid>()));
        }
        catch (Exception ex)
        {
            AddPbsPlaceholder(pbsNode, "PBS 加载失败：" + ex.Message);
            StatusText = "PBS 加载失败";
        }
    }

    /// <summary>按 PBS 节点分组当前项目的设备，用于填充装置下的“设备”文件夹。</summary>
    private async Task<IReadOnlyDictionary<Guid, List<EquipmentDto>>> LoadEquipmentsByPbsNodeAsync()
    {
        if (_session.Project is not { } project)
        {
            return new Dictionary<Guid, List<EquipmentDto>>();
        }

        var result = await _session.Api.GetEquipmentsAsync(project.Id, pageSize: 500);
        return result.Items
            .Where(x => x.PbsNodeId is not null)
            .GroupBy(x => x.PbsNodeId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    private static ExplorerNode BuildPbsExplorerItem(
        PbsNodeDto node,
        IReadOnlyDictionary<Guid, List<PbsNodeDto>> childrenByParent,
        IReadOnlyDictionary<Guid, List<EquipmentDto>> equipmentByPbsNode,
        HashSet<Guid> path)
    {
        var item = new ExplorerNode
        {
            Key = "pbs:" + node.Id,
            Title = FormatPbsNodeTitle(node),
            Glyph = node.NodeType switch
            {
                "Area" => "▦",
                "Unit" => "▣",
                "Loop" => "◇",
                "Building" => "▤",
                "System" => "◇",
                "SubSystem" => "·",
                _ => "·"
            },
            IsFolder = true
        };

        // 防御性环检测：数据异常时不递归。
        if (!path.Add(node.Id))
        {
            return item;
        }

        var isUnit = string.Equals(node.NodeType, "Unit", StringComparison.OrdinalIgnoreCase);

        // 装置层级默认固定三个分类文件夹；设备文件夹填充当前装置下的设备位号。
        if (isUnit)
        {
            AddUnitDefaultFolders(item, node.Id, equipmentByPbsNode, childrenByParent, path);
        }

        if (childrenByParent.TryGetValue(node.Id, out var children))
        {
            foreach (var child in children
                         .OrderBy(x => x.SortOrder)
                         .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase))
            {
                // 装置的回路 / 建筑物节点已经归入对应分类文件夹，不再作为普通子节点重复显示。
                if (isUnit && IsCategoryChild(child.NodeType))
                {
                    continue;
                }

                item.Children.Add(BuildPbsExplorerItem(child, childrenByParent, equipmentByPbsNode, path));
            }
        }

        path.Remove(node.Id);
        return item;
    }

    /// <summary>
    /// 装置（Unit）下固定展示的分类文件夹。键包含装置 Id，保证跨装置唯一。
    /// </summary>
    private static void AddUnitDefaultFolders(
        ExplorerNode unitItem,
        Guid unitId,
        IReadOnlyDictionary<Guid, List<EquipmentDto>> equipmentByPbsNode,
        IReadOnlyDictionary<Guid, List<PbsNodeDto>> childrenByParent,
        HashSet<Guid> path)
    {
        var devicesFolder = new ExplorerNode
        {
            Key = $"pbs:{unitId}:devices",
            Title = "设备",
            Glyph = "\uED25",
            GlyphFontFamily = "Segoe MDL2 Assets",
            IsFolder = true,
            CategoryFolder = ExplorerCategoryFolder.Equipment,
            RelatedPbsNodeId = unitId
        };
        if (equipmentByPbsNode.TryGetValue(unitId, out var equipments))
        {
            foreach (var equipment in equipments
                         .OrderBy(x => x.TagNo, StringComparer.OrdinalIgnoreCase))
            {
                devicesFolder.Children.Add(new ExplorerNode
                {
                    Key = "equipment:" + equipment.Id,
                    Title = equipment.TagNo,
                    Glyph = "\uE950",
                    GlyphFontFamily = "Segoe MDL2 Assets"
                });
            }
        }

        unitItem.Children.Add(devicesFolder);
        var loopsFolder = new ExplorerNode
        {
            Key = $"pbs:{unitId}:loops",
            CategoryFolder = ExplorerCategoryFolder.Loop,
            RelatedPbsNodeId = unitId,
            Title = "回路",
            Glyph = "\uED25",
            GlyphFontFamily = "Segoe MDL2 Assets",
            IsFolder = true
        };
        AddCategoryChildren(loopsFolder, unitId, "Loop", childrenByParent, equipmentByPbsNode, path);
        unitItem.Children.Add(loopsFolder);
        var buildingsFolder = new ExplorerNode
        {
            Key = $"pbs:{unitId}:buildings",
            CategoryFolder = ExplorerCategoryFolder.Building,
            RelatedPbsNodeId = unitId,
            Title = "建筑物",
            Glyph = "\uED25",
            GlyphFontFamily = "Segoe MDL2 Assets",
            IsFolder = true
        };
        AddCategoryChildren(buildingsFolder, unitId, "Building", childrenByParent, equipmentByPbsNode, path);
        unitItem.Children.Add(buildingsFolder);
    }

    /// <summary>把装置下指定 PBS 类型的子节点放入对应分类文件夹。</summary>
    private static void AddCategoryChildren(
        ExplorerNode folder,
        Guid unitId,
        string nodeType,
        IReadOnlyDictionary<Guid, List<PbsNodeDto>> childrenByParent,
        IReadOnlyDictionary<Guid, List<EquipmentDto>> equipmentByPbsNode,
        HashSet<Guid> path)
    {
        if (!childrenByParent.TryGetValue(unitId, out var children))
        {
            return;
        }

        foreach (var child in children
                     .Where(x => string.Equals(x.NodeType, nodeType, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(x => x.SortOrder)
                     .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase))
        {
            folder.Children.Add(BuildPbsExplorerItem(child, childrenByParent, equipmentByPbsNode, path));
        }
    }

    /// <summary>
    /// PBS 节点标题：工厂根节点、装置与建筑物只显示编号，其余层级显示「编号 名称」。
    /// </summary>
    private static string FormatPbsNodeTitle(PbsNodeDto node) =>
        IsCodeOnlyNodeType(node.NodeType)
            ? node.Code
            : node.Code + "  " + node.Name;

    private static bool IsCodeOnlyNodeType(string nodeType) =>
        string.Equals(nodeType, "Unit", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(nodeType, "Building", StringComparison.OrdinalIgnoreCase);

    private static bool IsCategoryChild(string nodeType) =>
        nodeType.Equals("Loop", StringComparison.OrdinalIgnoreCase) ||
        nodeType.Equals("Building", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 项目未设置 PBS 根节点（Id 为空）时，项目放在工厂根节点上，范围为整个工厂：
    /// 显示工厂全部顶层 PBS 节点及其子树。
    /// </summary>
    private static void ApplyMissingPbsRoot(
        ExplorerNode pbsNode,
        IReadOnlyList<PbsNodeDto> plantNodes,
        IReadOnlyDictionary<Guid, List<EquipmentDto>> equipmentByPbsNode)
    {
        var roots = plantNodes
            .Where(x => x.ParentId is null)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (roots.Count == 0)
        {
            AddPbsPlaceholder(pbsNode, "当前工厂尚未建立 PBS 节点");
            return;
        }

        var childrenByParent = plantNodes
            .Where(x => x.ParentId is not null)
            .GroupBy(x => x.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var root in roots)
        {
            pbsNode.Children.Add(BuildPbsExplorerItem(
                root, childrenByParent, equipmentByPbsNode, new HashSet<Guid>()));
        }
    }

    private static void AddPbsPlaceholder(ExplorerNode pbsNode, string message) =>
        pbsNode.Children.Add(new ExplorerNode
        {
            Key = "pbs-placeholder",
            Title = message,
            Glyph = "⚠"
        });

    [RelayCommand]
    private void OpenIndex() =>
        OpenDocument("index", "索引表", () => new EquipmentIndexViewModel(_session, _projectContext));

    [RelayCommand]
    private void OpenSpecs() =>
        OpenDocument("specs", "规格书", () => new EquipmentSpecIndexViewModel(_session, _projectContext));

    [RelayCommand]
    private void OpenMaterials() =>
        OpenDocument("materials", "材料表", () => new MaterialIndexViewModel(_session, _projectContext));

    [RelayCommand]
    private void OpenTelecom() =>
        OpenDocument("telecom", "电信统计", () => new TelecomStatsViewModel(_session, _projectContext));

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

    public void OpenExplorerNode(ExplorerNode? node) => node?.Open?.Invoke();

    [RelayCommand]
    private void OpenSelectedProjectNode() => OpenExplorerNode(SelectedProjectNode);

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

        await LoadPbsTreeAsync();
        StatusText = "已刷新 " + DateTime.Now.ToString("HH:mm:ss");
    }

    /// <summary>当前右键是否位于装置下的“设备”文件夹。</summary>
    public bool IsEquipmentFolderSelected => SelectedPbsNode?.IsEquipmentFolder == true;

    /// <summary>当前右键是否位于装置下的分类文件夹（设备 / 回路 / 建筑物）。</summary>
    public bool IsCategoryFolderSelected => SelectedPbsNode?.IsCategoryFolder == true;

    partial void OnSelectedPbsNodeChanged(ExplorerNode? value)
    {
        OnPropertyChanged(nameof(IsEquipmentFolderSelected));
        OnPropertyChanged(nameof(IsCategoryFolderSelected));
    }

    /// <summary>
    /// 分类文件夹右键「新增」：设备走设备属性弹窗，回路 / 建筑物在装置下新增对应 PBS 节点。
    /// </summary>
    [RelayCommand]
    private async Task AddCategoryItemAsync()
    {
        var folder = SelectedPbsNode;
        if (folder?.IsCategoryFolder != true || folder.RelatedPbsNodeId is not Guid unitId)
        {
            StatusText = "请在装置下的分类文件夹上新增";
            return;
        }

        switch (folder.CategoryFolder)
        {
            case ExplorerCategoryFolder.Equipment:
                await AddEquipmentAsync();
                break;
            case ExplorerCategoryFolder.Loop:
                await AddUnitCategoryChildAsync(unitId, "Loop", "回路");
                break;
            case ExplorerCategoryFolder.Building:
                await AddUnitCategoryChildAsync(unitId, "Building", "建筑物");
                break;
        }
    }

    private async Task AddUnitCategoryChildAsync(Guid unitId, string nodeType, string title)
    {
        var unit = _pbsNodes.FirstOrDefault(x => x.Id == unitId);
        var unitTitle = unit is null ? string.Empty : unit.Code + "  " + unit.Name;
        var category = nodeType == "Loop" ? ExplorerCategoryFolder.Loop : ExplorerCategoryFolder.Building;

        var result = _categoryDialogs.EditCategory(
            new CategoryEditorContext(category, unitTitle, null, null, null, IsReadOnly: false));
        if (result is null)
        {
            return;
        }

        try
        {
            await _session.Api.CreateUnitChildAsync(
                unitId,
                new CreateUnitChildRequest(result.Code, result.Name, nodeType, result.Description));
            StatusText = title + "已创建";
            await LoadPbsTreeAsync();
        }
        catch (ApiException ex)
        {
            StatusText = "新增" + title + "失败：" + ex.Message;
        }
        catch (Exception ex)
        {
            StatusText = "新增" + title + "失败：" + ex.Message;
        }
    }

    /// <summary>在装置下的“设备”文件夹右键新增设备，保存流程与索引表一致。</summary>
    [RelayCommand]
    private async Task AddEquipmentAsync()
    {
        var project = _session.Project;
        var unitId = SelectedPbsNode?.RelatedPbsNodeId;
        if (project is null || SelectedPbsNode?.IsEquipmentFolder != true || unitId is null)
        {
            StatusText = "请在装置下的“设备”文件夹上新增设备";
            return;
        }

        try
        {
            var types = await _session.Api.GetDictionaryItemsAsync("EQUIPMENT_TYPE");
            var editorContext = new EquipmentEditorContext(
                null,
                false,
                BuildPbsOptions(_pbsNodes),
                types.ToList(),
                DefaultPbsNodeId: unitId);

            var saved = false;
            _equipmentDialogs.EditEquipment(editorContext, async request =>
            {
                try
                {
                    await _session.Api.CreateEquipmentAsync(project.Id, request);
                    saved = true;
                    return null;
                }
                catch (ApiException ex)
                {
                    return ex.Message;
                }
                catch (Exception ex)
                {
                    return "保存失败：" + ex.Message;
                }
            });

            if (!saved)
            {
                return;
            }

            StatusText = "设备已创建";
            await LoadPbsTreeAsync();
        }
        catch (Exception ex)
        {
            StatusText = "新增设备失败：" + ex.Message;
        }
    }

    private static List<PbsNodeOption> BuildPbsOptions(IReadOnlyList<PbsNodeDto> nodes)
    {
        var options = new List<PbsNodeOption>
        {
            new PbsNodeOption(null, "（未分配）")
        };
        var children = nodes
            .Where(x => x.ParentId is not null)
            .GroupBy(x => x.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
        var roots = nodes
            .Where(x => x.ParentId is null)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        void Walk(PbsNodeDto node, int depth)
        {
            var indent = depth == 0 ? string.Empty : new string('·', depth * 2) + " ";
            options.Add(new PbsNodeOption(node.Id, indent + node.Code + "  " + node.Name));
            if (!children.TryGetValue(node.Id, out var list))
            {
                return;
            }

            foreach (var child in list
                         .OrderBy(x => x.SortOrder)
                         .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase))
            {
                Walk(child, depth + 1);
            }
        }

        foreach (var root in roots)
        {
            Walk(root, 0);
        }

        return options;
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
    /// 工作区选择窗口已经换发新令牌后调用：重建项目上下文并重开默认文档。
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
        BuildExplorer();
        PbsTreeLoadTask = LoadPbsTreeAsync();
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
