using WinDesign.Client.ViewModels;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Numbering;
using WinDesign.Contracts.Pbs;

namespace WinDesign.DesignClient.ViewModels;

public partial class EquipmentIndexViewModel : ViewModelBase
{
    private readonly SessionService _session;
    private readonly ProjectContext _context;
    private readonly IEquipmentDialogService _dialogs;
    private NumberingRuleDto? _numberingRule;
    private IReadOnlyDictionary<Guid, string> _unitCodeByPbsNodeId =
        new Dictionary<Guid, string>();
    private IReadOnlyList<string> _unitCodeOptions = Array.Empty<string>();

    public ObservableCollection<EquipmentDto> Equipments { get; } = new();
    public ObservableCollection<PbsNodeOption> PbsFilterOptions { get; } = new();
    public ObservableCollection<PbsNodeOption> PbsEditorOptions { get; } = new();
    public ObservableCollection<DictionaryItemDto> EquipmentTypes { get; } = new();

    public IReadOnlyList<CodeNameOption> FilterDisciplineOptions { get; } = new[]
    {
        new CodeNameOption("", "全部专业"),
        new CodeNameOption("Instrument", "仪表"),
        new CodeNameOption("Telecom", "电信"),
        new CodeNameOption("IT", "IT")
    };

    [ObservableProperty]
    private EquipmentDto? selectedEquipment;

    private IReadOnlyList<EquipmentDto> _selectedEquipments = Array.Empty<EquipmentDto>();

    /// <summary>勾选/多选的行集合；为空时回退到单选 <see cref="SelectedEquipment"/>。</summary>
    public IReadOnlyList<EquipmentDto> SelectedEquipments => CurrentSelection;

    private IReadOnlyList<EquipmentDto> CurrentSelection =>
        _selectedEquipments.Count > 0
            ? _selectedEquipments
            : SelectedEquipment is null
                ? Array.Empty<EquipmentDto>()
                : new[] { SelectedEquipment };

    public bool HasSelection => CurrentSelection.Count > 0;

    public bool IsSingleSelection => CurrentSelection.Count == 1;

    public bool IsMultipleSelection => CurrentSelection.Count > 1;

    /// <summary>多选时只保留删除，其余工具栏/菜单项禁用。</summary>
    public bool CanCreateNew => !IsMultipleSelection;

    public bool CanRefresh => !IsMultipleSelection;

    public bool CanWrite => _session.IsAdmin || _session.IsDesigner;

    public bool CanCreateSpec => IsSingleSelection && CanWrite;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string filterDiscipline = string.Empty;

    [ObservableProperty]
    private PbsNodeOption? filterPbsNode;

    [ObservableProperty]
    private string? statusMessage;

    public ProjectContext Context => _context;

    public EquipmentIndexViewModel(SessionService session, ProjectContext context, IEquipmentDialogService? dialogs = null)
    {
        _session = session;
        _context = context;
        _dialogs = dialogs ?? new EquipmentDialogService();
        Title = "索引表";
        _context.ProjectChanged += OnProjectChanged;
        _ = RefreshAsync();
    }

    public override void Dispose() => _context.ProjectChanged -= OnProjectChanged;

    /// <summary>由视图在 DataGrid.SelectionChanged 时调用，支持勾选多行。</summary>
    public void UpdateSelection(IEnumerable<EquipmentDto>? items)
    {
        _selectedEquipments = items?.Distinct().ToList() ?? new List<EquipmentDto>();
        if (_selectedEquipments.Count == 0 && SelectedEquipment is not null)
        {
            SelectedEquipment = null;
        }
        else if (_selectedEquipments.Count == 1 && !ReferenceEquals(SelectedEquipment, _selectedEquipments[0]))
        {
            SelectedEquipment = _selectedEquipments[0];
        }

        NotifySelectionState();
    }

    partial void OnSelectedEquipmentChanged(EquipmentDto? value) => NotifySelectionState();

    private void NotifySelectionState()
    {
        OnPropertyChanged(nameof(SelectedEquipments));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(IsSingleSelection));
        OnPropertyChanged(nameof(IsMultipleSelection));
        OnPropertyChanged(nameof(CanCreateNew));
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(CanCreateSpec));
    }

    public override Task RefreshAsync() => RunAsync(async () =>
    {
        var project = _context.CurrentProject;
        var plantId = _session.PlantId;
        if (project is null || plantId is null)
        {
            Equipments.Clear();
            PbsFilterOptions.Clear();
            PbsEditorOptions.Clear();
            EquipmentTypes.Clear();
            _numberingRule = null;
            _unitCodeByPbsNodeId = new Dictionary<Guid, string>();
            _unitCodeOptions = Array.Empty<string>();
            SelectedEquipment = null;
            return;
        }

        var nodes = await _session.Api.GetPbsNodesAsync(plantId.Value);
        BuildPbsOptions(nodes);
        _unitCodeOptions = BuildUnitCodeOptions(nodes, project.PbsRootNodeId);
        await LoadNumberingRuleAsync(project.Id);

        var types = await _session.Api.GetDictionaryItemsAsync("EQUIPMENT_TYPE");
        EquipmentTypes.Clear();
        foreach (var type in types)
        {
            EquipmentTypes.Add(type);
        }

        FilterPbsNode ??= PbsFilterOptions.FirstOrDefault();
        await LoadEquipmentsAsync(project.Id);
    });

    [RelayCommand]
    private Task SearchAsync() => RunAsync(async () =>
    {
        var project = _context.CurrentProject ?? throw new ApiException("请先选择工作区项目");
        await LoadEquipmentsAsync(project.Id);
    });

    [RelayCommand]
    private Task NewEquipmentAsync() => OpenEditorAsync(null, false);

    [RelayCommand]
    private async Task EditEquipmentAsync()
    {
        if (!IsSingleSelection || SelectedEquipment is null)
        {
            ErrorMessage = "请选择单台设备进行编辑";
            return;
        }

        await OpenEditorAsync(SelectedEquipment, false);
    }

    [RelayCommand]
    private async Task DuplicateEquipmentAsync()
    {
        if (!IsSingleSelection || SelectedEquipment is null)
        {
            ErrorMessage = "请选择单台设备进行复制";
            return;
        }

        await OpenEditorAsync(SelectedEquipment, true);
    }

    [RelayCommand]
    private Task DeleteEquipmentAsync() => RunAsync(async () =>
    {
        var targets = CurrentSelection.ToList();
        if (targets.Count == 0)
        {
            throw new ApiException("请选择要删除的设备");
        }

        var message = targets.Count == 1
            ? $"确定删除设备 {targets[0].TagNo} 吗？"
            : $"确定删除选中的 {targets.Count} 台设备吗？";
        if (!_dialogs.Confirm(message))
        {
            return;
        }

        foreach (var equipment in targets)
        {
            await _session.Api.DeleteEquipmentAsync(equipment.Id);
        }

        StatusMessage = targets.Count == 1 ? "设备已删除" : $"已删除 {targets.Count} 台设备";
        SelectedEquipment = null;
        UpdateSelection(Array.Empty<EquipmentDto>());
        var project = _context.CurrentProject;
        if (project is not null)
        {
            await LoadEquipmentsAsync(project.Id);
        }
    });

    /// <summary>在索引表右键为当前位号创建空白规格书，详细内容到规格书模块补充。</summary>
    [RelayCommand]
    private Task CreateSpecAsync() => RunAsync(async () =>
    {
        var project = _context.CurrentProject ?? throw new ApiException("请先选择工作区项目");
        if (!CanWrite)
        {
            throw new ApiException("当前账号没有创建规格书的权限");
        }
        if (!IsSingleSelection || SelectedEquipment is null)
        {
            throw new ApiException("请选择单台设备创建规格书");
        }

        var equipment = SelectedEquipment;
        var existing = await _session.Api.GetEquipmentSpecsAsync(
            project.Id, equipmentId: equipment.Id, pageSize: 1);
        if (existing.Items.Count > 0)
        {
            throw new ApiException($"{equipment.TagNo} 已有规格书，请到「规格书」模块编辑");
        }

        // 规格书编号：位号-SP；名称：位号 规格书。
        var codeBase = equipment.TagNo.Length > 61 ? equipment.TagNo[..61] : equipment.TagNo;
        var specName = equipment.TagNo + " 规格书";
        if (specName.Length > 200)
        {
            specName = specName[..200];
        }

        await _session.Api.CreateEquipmentSpecAsync(equipment.Id, new EquipmentSpecUpsertRequest
        {
            SpecCode = codeBase + "-SP",
            SpecName = specName,
            Parameters = Array.Empty<SpecParameterDto>()
        });
        StatusMessage = $"已为 {equipment.TagNo} 创建空白规格书，请到「规格书」模块补充";
    });

    private async Task OpenEditorAsync(EquipmentDto? equipment, bool isCopy)
    {
        var project = _context.CurrentProject;
        if (project is null)
        {
            ErrorMessage = "请先选择工作区项目";
            return;
        }

        EquipmentDto? saved = null;
        // 登录时选择的装置作为新建设备的默认 PBS 与默认位号前缀。
        var defaultPbsNodeId = _session.UnitId is Guid unitId &&
                               PbsEditorOptions.Any(x => x.Id == unitId)
            ? unitId
            : (Guid?)null;
        var context = new EquipmentEditorContext(
            equipment,
            isCopy,
            PbsEditorOptions.ToList(),
            EquipmentTypes.ToList(),
            DefaultPbsNodeId: defaultPbsNodeId,
            NumberingRule: _numberingRule,
            UnitCodeByPbsNodeId: _unitCodeByPbsNodeId,
            UnitCodeOptions: _unitCodeOptions,
            DefaultUnitCode: _session.UnitCode);

        // 保存放在弹窗的「确定」流程里：服务端拒绝（如位号重复）时错误显示在弹窗内，弹窗不关闭。
        var request = _dialogs.EditEquipment(context, async saveRequest =>
        {
            try
            {
                if (equipment is null || isCopy)
                {
                    saved = await _session.Api.CreateEquipmentAsync(project.Id, saveRequest);
                    StatusMessage = isCopy ? "设备已复制" : "设备已创建";
                }
                else
                {
                    saved = await _session.Api.UpdateEquipmentAsync(equipment.Id, saveRequest);
                    StatusMessage = "设备已保存";
                }

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

        if (request is null || saved is null)
        {
            return;
        }

        var savedId = saved.Id;
        await RunAsync(async () =>
        {
            await LoadEquipmentsAsync(project.Id);
            SelectedEquipment = Equipments.FirstOrDefault(x => x.Id == savedId);
        });
    }

    private void OnProjectChanged(object? sender, EventArgs e)
    {
        _ = RefreshAsync();
    }

    private async Task LoadEquipmentsAsync(Guid projectId)
    {
        var result = await _session.Api.GetEquipmentsAsync(
            projectId,
            SearchText,
            NullIfEmpty(FilterDiscipline),
            FilterPbsNode?.Id);

        var currentId = SelectedEquipment?.Id;
        Equipments.Clear();
        foreach (var equipment in result.Items)
        {
            Equipments.Add(equipment);
        }
        if (currentId is not null)
        {
            SelectedEquipment = Equipments.FirstOrDefault(x => x.Id == currentId);
        }
    }

    private void BuildPbsOptions(IReadOnlyList<PbsNodeDto> nodes)
    {
        BuildUnitCodeMap(nodes);
        PbsFilterOptions.Clear();
        PbsEditorOptions.Clear();
        PbsFilterOptions.Add(new PbsNodeOption(null, "全部 PBS"));
        PbsEditorOptions.Add(new PbsNodeOption(null, "（未分配）"));

        var children = new Dictionary<Guid, List<PbsNodeDto>>();
        foreach (var node in nodes)
        {
            var parentKey = node.ParentId ?? Guid.Empty;
            if (!children.TryGetValue(parentKey, out var list))
            {
                list = new List<PbsNodeDto>();
                children[parentKey] = list;
            }
            list.Add(node);
        }
        foreach (var list in children.Values)
        {
            list.Sort((a, b) =>
            {
                var order = a.SortOrder.CompareTo(b.SortOrder);
                return order != 0 ? order : string.Compare(a.Code, b.Code, StringComparison.OrdinalIgnoreCase);
            });
        }

        void Walk(Guid? parentId, int depth)
        {
            if (!children.TryGetValue(parentId ?? Guid.Empty, out var list))
            {
                return;
            }
            foreach (var node in list)
            {
                var indent = depth == 0 ? string.Empty : new string('·', depth * 2) + " ";
                var display = indent + node.Code + "  " + node.Name;
                PbsFilterOptions.Add(new PbsNodeOption(node.Id, display));
                PbsEditorOptions.Add(new PbsNodeOption(node.Id, display));
                Walk(node.Id, depth + 1);
            }
        }

        Walk(null, 0);
    }

    /// <summary>加载当前项目的编号规定，供新建/复制设备时按规则拼装位号。</summary>
    private async Task LoadNumberingRuleAsync(Guid projectId)
    {
        try
        {
            _numberingRule = await _session.Api.GetNumberingRuleAsync(projectId);
        }
        catch
        {
            // 编号规定读取失败不应阻断索引表；回退为自由输入位号。
            _numberingRule = null;
        }
    }

    /// <summary>PBS 节点 → 最近上级装置编号，用于位号前缀自动带出。</summary>
    private void BuildUnitCodeMap(IReadOnlyList<PbsNodeDto> nodes)
    {
        var byId = nodes.ToDictionary(x => x.Id);
        string? FindUnitCode(Guid nodeId)
        {
            var current = byId[nodeId];
            var visited = new HashSet<Guid>();
            while (visited.Add(current.Id))
            {
                if (string.Equals(current.NodeType, "Unit", StringComparison.OrdinalIgnoreCase))
                {
                    return current.Code;
                }
                if (current.ParentId is Guid parentId && byId.TryGetValue(parentId, out var parent))
                {
                    current = parent;
                }
                else
                {
                    return null;
                }
            }
            return null;
        }

        var map = new Dictionary<Guid, string>();
        foreach (var node in nodes)
        {
            var code = FindUnitCode(node.Id);
            if (!string.IsNullOrWhiteSpace(code))
            {
                map[node.Id] = code;
            }
        }
        _unitCodeByPbsNodeId = map;
    }

    /// <summary>当前项目 PBS 范围内的装置编号清单，供位号前缀下拉选择。</summary>
    private static IReadOnlyList<string> BuildUnitCodeOptions(
        IReadOnlyList<PbsNodeDto> nodes,
        Guid? projectRootId)
    {
        var byId = nodes.ToDictionary(x => x.Id);
        bool InScope(PbsNodeDto node)
        {
            if (projectRootId is not Guid rootId)
            {
                return true;
            }

            var current = node;
            var visited = new HashSet<Guid>();
            while (visited.Add(current.Id))
            {
                if (current.Id == rootId)
                {
                    return true;
                }
                if (current.ParentId is Guid parentId && byId.TryGetValue(parentId, out var parent))
                {
                    current = parent;
                }
                else
                {
                    return false;
                }
            }
            return false;
        }

        return nodes
            .Where(x => string.Equals(x.NodeType, "Unit", StringComparison.OrdinalIgnoreCase))
            .Where(InScope)
            .Select(x => x.Code)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
