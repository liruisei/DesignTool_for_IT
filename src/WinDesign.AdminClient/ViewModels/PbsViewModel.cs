using WinDesign.AdminClient.Services;
using WinDesign.Client.ViewModels;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Plants;

namespace WinDesign.AdminClient.ViewModels;

public partial class PbsNodeItem : ObservableObject
{
    public PbsNodeItem(PbsNodeDto node)
    {
        Node = node;
    }

    public PbsNodeItem(EquipmentDto equipment)
    {
        Equipment = equipment;
    }

    /// <summary>工厂本身作为 PBS 树的根节点。</summary>
    public PbsNodeItem(PlantDto plant)
    {
        Plant = plant;
    }

    public PbsNodeDto? Node { get; }
    public EquipmentDto? Equipment { get; }
    public PlantDto? Plant { get; }

    public bool IsNode => Node is not null;
    public bool IsEquipment => Equipment is not null;
    public bool IsPlant => Plant is not null;
    public string HeaderCode => Plant?.Code ?? Node?.Code ?? Equipment?.TagNo ?? string.Empty;
    public string HeaderName => Plant?.Name ?? Node?.Name ?? Equipment?.Name ?? string.Empty;

    public string Glyph => IsPlant
        ? "⌂"
        : IsEquipment
            ? "◦"
            : Node?.NodeType switch
            {
                "Root" => "⌂",
                "Area" => "▦",
                "Unit" => "▣",
                "Loop" => "◇",
                "System" => "◇",
                "SubSystem" => "·",
                _ => "·"
            };

    public ObservableCollection<PbsNodeItem> Children { get; } = new();

    [ObservableProperty]
    private bool isExpanded = true;

    [ObservableProperty]
    private bool isSelected;
}

public sealed record PbsNodeTypeOption(string Code, string Name);

public partial class PbsViewModel : ViewModelBase
{
    private readonly SessionService _session;
    private readonly ProjectContext _context;
    private readonly IPbsDialogService _dialogs;
    private readonly IEquipmentDialogService _equipmentDialogs;
    private Dictionary<Guid, PbsNodeDto> _nodes = new();

    public ObservableCollection<PbsNodeItem> Roots { get; } = new();
    public ObservableCollection<PbsNodeOption> ParentOptions { get; } = new();
    public ObservableCollection<PbsNodeOption> PbsEditorOptions { get; } = new();
    public ObservableCollection<DictionaryItemDto> EquipmentTypes { get; } = new();
    public ObservableCollection<PlantDto> Plants { get; } = new();

    public IReadOnlyList<PbsNodeTypeOption> NodeTypeOptions { get; } = new[]
    {
        new PbsNodeTypeOption("Area", "区域"),
        new PbsNodeTypeOption("Unit", "装置"),
        new PbsNodeTypeOption("Loop", "回路"),
        new PbsNodeTypeOption("System", "系统"),
        new PbsNodeTypeOption("SubSystem", "子系统")
    };

    [ObservableProperty]
    private PbsNodeItem? selectedNode;

    [ObservableProperty]
    private PlantDto? selectedPlant;

    [ObservableProperty]
    private string? statusMessage;

    public ProjectContext Context => _context;

    /// <summary>管理模块下可在多个工厂之间切换并维护工厂本身。</summary>
    public bool IsManagementMode => _session.IsManagementMode;
    public bool CanManagePlants => _session.IsManagementMode;
    public bool CanAddEquipment => _context.HasProject;
    public bool ShowProjectContext => !_session.IsManagementMode;
    public string SelectedPlantName => SelectedPlant?.Name ?? "未选择工厂";

    /// <summary>右键当前选中的是工厂根节点。</summary>
    public bool IsFactoryNodeSelected => SelectedNode?.IsPlant == true;
    /// <summary>右键当前选中的是工厂下的 PBS 节点。</summary>
    public bool IsPbsNodeSelected => SelectedNode?.Node is not null;
    /// <summary>只有工厂根节点上才提供工厂的新建/编辑/删除。</summary>
    public bool CanManageFactoriesHere => CanManagePlants && IsFactoryNodeSelected;
    public string ProjectName => _session.Project?.Name ?? "未选择项目";

    /// <summary>右键菜单：设备节点没有子节点；工厂与 PBS 节点可以新建。</summary>
    public bool CanCreateChild => SelectedNode is not null && SelectedNode.Equipment is null;

    /// <summary>右键菜单：工厂仅管理员可修改，节点/设备均可修改。</summary>
    public bool CanEditSelectedNode =>
        SelectedNode is not null && (SelectedNode.IsPlant != true || CanManagePlants);

    /// <summary>右键菜单：工厂仅管理员可删除，节点/设备均可删除。</summary>
    public bool CanDeleteSelectedNode =>
        SelectedNode is not null && (SelectedNode.IsPlant != true || CanManagePlants);


    public PbsViewModel(
        SessionService session,
        ProjectContext context,
        IPbsDialogService? dialogs = null,
        IEquipmentDialogService? equipmentDialogs = null)
    {
        _session = session;
        _context = context;
        _dialogs = dialogs ?? new PbsDialogService();
        _equipmentDialogs = equipmentDialogs ?? new EquipmentDialogService();
        Title = "PBS管理";
        _context.ProjectChanged += OnProjectChanged;
        _ = RefreshAsync();
    }

    public override void Dispose() => _context.ProjectChanged -= OnProjectChanged;

    public override Task RefreshAsync() => RunAsync(async () =>
    {
        await LoadPlantsAsync();
        await LoadTreeAsync();
    });

    private async Task LoadPlantsAsync()
    {
        var currentId = SelectedPlant?.Id ?? _session.PlantId;
        var plants = await _session.Api.GetPlantsAsync();

        // 工作区模式只能看到当前工厂；管理模块可以看到全部工厂。
        var accessible = _session.IsManagementMode
            ? plants
            : plants.Where(x => x.Id == _session.PlantId).ToList();

        Plants.Clear();
        foreach (var plant in accessible)
        {
            Plants.Add(plant);
        }

        SelectedPlant = Plants.FirstOrDefault(x => x.Id == currentId)
                        ?? Plants.FirstOrDefault();
    }

    private async Task LoadTreeAsync()
    {
        if (Plants.Count == 0)
        {
            Roots.Clear();
            ParentOptions.Clear();
            PbsEditorOptions.Clear();
            EquipmentTypes.Clear();
            SelectedNode = null;
            return;
        }

        var types = await _session.Api.GetDictionaryItemsAsync("EQUIPMENT_TYPE");
        EquipmentTypes.Clear();
        foreach (var type in types)
        {
            EquipmentTypes.Add(type);
        }

        // 管理模块没有项目范围，只显示 PBS 结构；工作区模式显示项目范围内的设备。
        IReadOnlyList<EquipmentDto> equipments = Array.Empty<EquipmentDto>();
        if (!_session.IsManagementMode && _context.CurrentProject is { } project)
        {
            equipments = (await _session.Api.GetEquipmentsAsync(project.Id, pageSize: 2000)).Items;
        }

        var nodesByPlant = new Dictionary<Guid, IReadOnlyList<PbsNodeDto>>();
        var allNodes = new List<PbsNodeDto>();
        foreach (var plant in Plants)
        {
            var nodes = await _session.Api.GetPbsNodesAsync(plant.Id);
            nodesByPlant[plant.Id] = nodes;
            allNodes.AddRange(nodes);
        }

        _nodes = allNodes.ToDictionary(x => x.Id);
        BuildTree(Plants.ToList(), nodesByPlant, equipments);
        BuildParentOptions();

        if (SelectedNode?.IsPlant == true)
        {
            SelectedNode = FindPlantItem(Roots, SelectedNode.Plant!.Id)
                           ?? Roots.FirstOrDefault();
        }
        else if (SelectedNode?.Node is { } selectedNode && _nodes.ContainsKey(selectedNode.Id))
        {
            SelectedNode = FindNodeItem(Roots, selectedNode.Id);
        }
        else if (SelectedNode?.Equipment is { } selectedEquipment)
        {
            SelectedNode = FindEquipmentItem(Roots, selectedEquipment.Id);
        }
        else
        {
            SelectedNode = Roots.FirstOrDefault();
        }
    }

    partial void OnSelectedPlantChanged(PlantDto? value) =>
        OnPropertyChanged(nameof(SelectedPlantName));

    partial void OnSelectedNodeChanged(PbsNodeItem? value)
    {
        OnPropertyChanged(nameof(IsFactoryNodeSelected));
        OnPropertyChanged(nameof(IsPbsNodeSelected));
        OnPropertyChanged(nameof(CanManageFactoriesHere));
        OnPropertyChanged(nameof(CanCreateChild));
        OnPropertyChanged(nameof(CanEditSelectedNode));
        OnPropertyChanged(nameof(CanDeleteSelectedNode));

        // 树中选中哪个工厂下的节点，后续新建/编辑就以该工厂为上下文。
        var plantId = value?.Plant?.Id
                      ?? value?.Node?.PlantId
                      ?? value?.Equipment?.PlantId;
        if (plantId is not Guid id)
        {
            return;
        }

        var plant = Plants.FirstOrDefault(x => x.Id == id);
        if (plant is not null)
        {
            SelectedPlant = plant;
        }
    }

    [RelayCommand]
    private Task NewPlantAsync() => RunAsync(async () =>
    {
        if (!CanManagePlants)
        {
            return;
        }

        var result = _dialogs.EditPlant(new PlantEditorContext(null));
        if (result is null)
        {
            return;
        }

        var saved = await _session.Api.CreatePlantAsync(new CreatePlantRequest(
            result.Code,
            result.Name,
            result.ClientName,
            result.Location,
            result.Description));
        await RefreshAsync();
        SelectedPlant = Plants.FirstOrDefault(x => x.Id == saved.Id);
        SelectedNode = FindPlantItem(Roots, saved.Id);
        StatusMessage = "工厂已创建";
    });

    [RelayCommand]
    private Task EditPlantAsync() => RunAsync(async () =>
    {
        if (!CanManagePlants || SelectedPlant is null)
        {
            ErrorMessage = "请先选择工厂";
            return;
        }

        var plant = SelectedPlant;
        var result = _dialogs.EditPlant(new PlantEditorContext(plant));
        if (result is null)
        {
            return;
        }

        var saved = await _session.Api.UpdatePlantAsync(plant.Id, new UpdatePlantRequest(
            result.Name,
            result.ClientName,
            result.Location,
            result.Description,
            result.IsActive));
        await RefreshAsync();
        SelectedPlant = Plants.FirstOrDefault(x => x.Id == saved.Id);
        SelectedNode = FindPlantItem(Roots, saved.Id);
        StatusMessage = "工厂已保存";
    });

    [RelayCommand]
    private Task DeletePlantAsync() => RunAsync(DeletePlantCoreAsync);

    private async Task DeletePlantCoreAsync()
    {
        if (!CanManagePlants || SelectedPlant is null)
        {
            ErrorMessage = "请先选择工厂";
            return;
        }

        var plant = SelectedPlant;
        if (!_dialogs.Confirm(
                $"确定删除工厂“{plant.Code} {plant.Name}”吗？工厂下必须没有项目、设备和 PBS 节点。"))
        {
            return;
        }

        await _session.Api.DeletePlantAsync(plant.Id);
        await RefreshAsync();
        SelectedNode = Roots.FirstOrDefault(x => x.IsPlant);
        StatusMessage = "工厂已删除";
    }

    [RelayCommand]
    private Task NewTopLevelAsync() => OpenEditorAsync(null, null, "Area");

    private static string NextNodeType(string nodeType) => nodeType switch
    {
        "Area" => "Unit",
        "Unit" => "Loop",
        "Loop" => "System",
        "System" => "SubSystem",
        _ => "SubSystem"
    };

    [RelayCommand]
    private async Task NewChildAsync()
    {
        if (SelectedNode?.IsPlant == true)
        {
            // 在工厂根节点下新建的就是顶层区域。
            await OpenEditorAsync(null, null, "Area");
            return;
        }

        if (SelectedNode?.Node is null)
        {
            ErrorMessage = "请先选择一个 PBS 节点";
            return;
        }

        await OpenEditorAsync(
            null,
            SelectedNode.Node.Id,
            NextNodeType(SelectedNode.Node.NodeType));
    }

    [RelayCommand]
    private async Task NewSiblingAsync()
    {
        if (SelectedNode?.IsPlant == true)
        {
            ErrorMessage = "工厂根节点没有同级节点，请使用“新建区域”";
            return;
        }

        if (SelectedNode?.Node is null)
        {
            ErrorMessage = "请先选择一个 PBS 节点";
            return;
        }
        // 顶层节点（ParentId 为空）也可以新建同级，即工厂下的另一个区域。
        await OpenEditorAsync(null, SelectedNode.Node.ParentId, SelectedNode.Node.NodeType);
    }

    [RelayCommand]
    private async Task EditNodeAsync()
    {
        if (SelectedNode?.IsPlant == true)
        {
            if (CanManagePlants)
            {
                await EditPlantAsync();
            }
            else
            {
                ErrorMessage = "工厂信息请由管理员在管理模块中维护";
            }
            return;
        }

        if (SelectedNode?.Equipment is not null)
        {
            await EditEquipmentAsync();
            return;
        }
        if (SelectedNode?.Node is null)
        {
            ErrorMessage = "请选择要编辑的节点或设备";
            return;
        }

        await OpenEditorAsync(SelectedNode.Node, null);
    }

    [RelayCommand]
    private Task DeleteNodeAsync() => RunAsync(async () =>
    {
        if (SelectedNode?.IsPlant == true)
        {
            if (!CanManagePlants)
            {
                throw new ApiException("工厂信息请由管理员在管理模块中维护");
            }

            // 右键菜单只有“删除节点”一个入口，工厂根节点复用工厂删除逻辑。
            await DeletePlantCoreAsync();
            return;
        }

        if (SelectedNode?.Equipment is not null)
        {
            await DeleteEquipmentCoreAsync(SelectedNode.Equipment);
            return;
        }
        if (SelectedNode?.Node is null)
        {
            throw new ApiException("请选择要删除的节点或设备");
        }

        if (!_dialogs.Confirm($"确定删除节点“{SelectedNode.Node.Code} {SelectedNode.Node.Name}”吗？"))
        {
            return;
        }

        await _session.Api.DeletePbsNodeAsync(SelectedNode.Node.Id);
        StatusMessage = "节点已删除";
        SelectedNode = null;
        await RefreshAsync();
    });

    [RelayCommand]
    private Task AddEquipmentAsync() => RunAsync(async () =>
    {
        var project = _context.CurrentProject ?? throw new ApiException(
            _session.IsManagementMode
                ? "管理模块下不能添加设备，请选择工厂和项目进入工作区后再操作"
                : "请先选择工作区项目");
        var pbsNodeId = SelectedNode?.Node?.Id ?? SelectedNode?.Equipment?.PbsNodeId;
        if (pbsNodeId is null)
        {
            throw new ApiException("请选择一个装置或回路节点");
        }

        var context = new EquipmentEditorContext(
            null,
            false,
            PbsEditorOptions.ToList(),
            EquipmentTypes.ToList(),
            pbsNodeId,
            FindLoopNo(pbsNodeId.Value));
        var request = _equipmentDialogs.EditEquipment(context);
        if (request is null)
        {
            return;
        }

        var saved = await _session.Api.CreateEquipmentAsync(project.Id, request);
        await RefreshAsync();
        SelectedNode = FindEquipmentItem(Roots, saved.Id);
        StatusMessage = "设备已创建并写入索引表";
    });

    [RelayCommand]
    private void ExpandAll() => SetExpanded(true);

    [RelayCommand]
    private void CollapseAll() => SetExpanded(false);

    private async Task EditEquipmentAsync()
    {
        var equipment = SelectedNode?.Equipment;
        if (equipment is null)
        {
            ErrorMessage = "请选择要编辑的设备";
            return;
        }

        var context = new EquipmentEditorContext(
            equipment,
            false,
            PbsEditorOptions.ToList(),
            EquipmentTypes.ToList());
        var request = _equipmentDialogs.EditEquipment(context);
        if (request is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var saved = await _session.Api.UpdateEquipmentAsync(equipment.Id, request);
            await RefreshAsync();
            SelectedNode = FindEquipmentItem(Roots, saved.Id);
            StatusMessage = "设备已保存";
        });
    }

    private async Task DeleteEquipmentCoreAsync(EquipmentDto equipment)
    {
        if (!_dialogs.Confirm($"确定删除设备 {equipment.TagNo} 吗？"))
        {
            return;
        }

        await _session.Api.DeleteEquipmentAsync(equipment.Id);
        StatusMessage = "设备已删除";
        SelectedNode = null;
        await RefreshAsync();
    }

    private async Task OpenEditorAsync(
        PbsNodeDto? node,
        Guid? defaultParentId,
        string? defaultNodeType = null)
    {
        var plantId = SelectedPlant?.Id;
        if (plantId is null)
        {
            ErrorMessage = "请先选择工厂";
            return;
        }

        var editorContext = new PbsNodeEditorContext(
            node, defaultParentId, ParentOptions.ToList(), defaultNodeType);
        var result = _dialogs.EditPbsNode(editorContext);
        if (result is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            PbsNodeDto saved;
            if (node is null)
            {
                saved = await _session.Api.CreatePbsNodeAsync(plantId.Value, new CreatePbsNodeRequest(
                    result.ParentId,
                    result.Code,
                    result.Name,
                    result.NodeType,
                    result.SortOrder,
                    result.Description));
                StatusMessage = "节点已创建";
            }
            else
            {
                saved = await _session.Api.UpdatePbsNodeAsync(node.Id, new UpdatePbsNodeRequest(
                    result.ParentId,
                    result.Code,
                    result.Name,
                    result.NodeType,
                    result.SortOrder,
                    result.Description));
                StatusMessage = "节点已保存";
            }

            await RefreshAsync();
            SelectedNode = FindNodeItem(Roots, saved.Id);
        });
    }

    private void OnProjectChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CanAddEquipment));
        _ = RefreshAsync();
    }

    private void BuildTree(
        IReadOnlyList<PlantDto> plants,
        IReadOnlyDictionary<Guid, IReadOnlyList<PbsNodeDto>> nodesByPlant,
        IReadOnlyList<EquipmentDto> equipments)
    {
        Roots.Clear();

        // 每个工厂都是树的一个根节点，全部同时显示、可独立展开/折叠。
        foreach (var plant in plants)
        {
            var plantRoot = new PbsNodeItem(plant) { IsExpanded = true };
            if (!nodesByPlant.TryGetValue(plant.Id, out var nodes))
            {
                Roots.Add(plantRoot);
                continue;
            }

            var map = nodes.ToDictionary(x => x.Id, x => new PbsNodeItem(x));
            foreach (var item in map.Values
                         .OrderBy(x => x.Node!.SortOrder)
                         .ThenBy(x => x.Node!.Code, StringComparer.OrdinalIgnoreCase))
            {
                if (item.Node!.ParentId is Guid parentId && map.TryGetValue(parentId, out var parent))
                {
                    parent.Children.Add(item);
                }
                else
                {
                    plantRoot.Children.Add(item);
                }
            }

            var equipmentByNode = equipments
                .Where(x => x.PlantId == plant.Id && x.PbsNodeId is not null)
                .GroupBy(x => x.PbsNodeId!.Value);
            foreach (var group in equipmentByNode)
            {
                if (!map.TryGetValue(group.Key, out var parent))
                {
                    continue;
                }
                foreach (var equipment in group.OrderBy(x => x.TagNo, StringComparer.OrdinalIgnoreCase))
                {
                    parent.Children.Add(new PbsNodeItem(equipment));
                }
            }

            Roots.Add(plantRoot);
        }
    }

    private void BuildParentOptions()
    {
        ParentOptions.Clear();
        ParentOptions.Add(new PbsNodeOption(null, "（无 / 根节点）"));
        PbsEditorOptions.Clear();
        PbsEditorOptions.Add(new PbsNodeOption(null, "（未分配）"));

        void Walk(IEnumerable<PbsNodeItem> items, int depth)
        {
            foreach (var item in items)
            {
                if (item.Node is null)
                {
                    // 工厂根节点本身不是 PbsNode，继续向下收集区域/装置。
                    Walk(item.Children, depth);
                    continue;
                }

                var indent = depth == 0 ? string.Empty : new string('·', depth * 2) + " ";
                var display = indent + item.Node.Code + "  " + item.Node.Name;
                ParentOptions.Add(new PbsNodeOption(item.Node.Id, display));
                PbsEditorOptions.Add(new PbsNodeOption(item.Node.Id, display));
                Walk(item.Children, depth + 1);
            }
        }

        Walk(Roots, 0);
    }

    private string? FindLoopNo(Guid nodeId)
    {
        var current = _nodes.GetValueOrDefault(nodeId);
        var guard = 0;
        while (current is not null && guard++ < 200)
        {
            if (current.NodeType == "Loop")
            {
                return current.Code;
            }
            current = current.ParentId is Guid parentId ? _nodes.GetValueOrDefault(parentId) : null;
        }
        return null;
    }

    private static PbsNodeItem? FindPlantItem(IEnumerable<PbsNodeItem> items, Guid plantId)
    {
        foreach (var item in items)
        {
            if (item.Plant?.Id == plantId)
            {
                return item;
            }
            var found = FindPlantItem(item.Children, plantId);
            if (found is not null)
            {
                return found;
            }
        }
        return null;
    }

    private static PbsNodeItem? FindNodeItem(IEnumerable<PbsNodeItem> items, Guid id)
    {
        foreach (var item in items)
        {
            if (item.Node?.Id == id)
            {
                return item;
            }
            var found = FindNodeItem(item.Children, id);
            if (found is not null)
            {
                return found;
            }
        }
        return null;
    }

    private static PbsNodeItem? FindEquipmentItem(IEnumerable<PbsNodeItem> items, Guid id)
    {
        foreach (var item in items)
        {
            if (item.Equipment?.Id == id)
            {
                return item;
            }
            var found = FindEquipmentItem(item.Children, id);
            if (found is not null)
            {
                return found;
            }
        }
        return null;
    }

    private static void SetExpanded(PbsNodeItem item, bool expanded)
    {
        item.IsExpanded = expanded;
        foreach (var child in item.Children)
        {
            SetExpanded(child, expanded);
        }
    }

    private void SetExpanded(bool expanded)
    {
        foreach (var root in Roots)
        {
            SetExpanded(root, expanded);
        }
    }
}
