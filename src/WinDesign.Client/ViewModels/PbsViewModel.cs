using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Pbs;

namespace WinDesign.Client.ViewModels;

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

    public PbsNodeDto? Node { get; }
    public EquipmentDto? Equipment { get; }

    public bool IsNode => Node is not null;
    public bool IsEquipment => Equipment is not null;
    public string HeaderCode => Node?.Code ?? Equipment?.TagNo ?? string.Empty;
    public string HeaderName => Node?.Name ?? Equipment?.Name ?? string.Empty;
    public int EquipmentCount => Node?.EquipmentCount ?? 0;

    public string Glyph => IsEquipment
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

public sealed record PbsNodeOption(Guid? Id, string Display);

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

    public IReadOnlyList<PbsNodeTypeOption> NodeTypeOptions { get; } = new[]
    {
        new PbsNodeTypeOption("Root", "根节点"),
        new PbsNodeTypeOption("Area", "区域"),
        new PbsNodeTypeOption("Unit", "装置"),
        new PbsNodeTypeOption("Loop", "回路"),
        new PbsNodeTypeOption("System", "系统"),
        new PbsNodeTypeOption("SubSystem", "子系统")
    };

    [ObservableProperty]
    private PbsNodeItem? selectedNode;

    [ObservableProperty]
    private string? statusMessage;

    public ProjectContext Context => _context;

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
        Title = "工厂 PBS";
        _context.ProjectChanged += OnProjectChanged;
        _ = RefreshAsync();
    }

    public override Task RefreshAsync() => RunAsync(async () =>
    {
        var project = _context.CurrentProject;
        if (project is null)
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

        var nodes = await _session.Api.GetPbsNodesAsync(project.Id);
        var equipments = await _session.Api.GetEquipmentsAsync(project.Id, pageSize: 2000);
        _nodes = nodes.ToDictionary(x => x.Id);

        BuildTree(nodes, equipments.Items);
        BuildParentOptions();

        if (SelectedNode?.Node is { } selectedNode && _nodes.ContainsKey(selectedNode.Id))
        {
            SelectedNode = FindNodeItem(Roots, selectedNode.Id);
        }
        else if (SelectedNode?.Equipment is { } selectedEquipment)
        {
            SelectedNode = FindEquipmentItem(Roots, selectedEquipment.Id);
        }
    });

    [RelayCommand]
    private Task NewRootAsync() => OpenEditorAsync(null, null);

    [RelayCommand]
    private async Task NewChildAsync()
    {
        if (SelectedNode?.Node is null)
        {
            ErrorMessage = "请先选择一个 PBS 节点";
            return;
        }

        await OpenEditorAsync(null, SelectedNode.Node.Id);
    }

    [RelayCommand]
    private async Task NewSiblingAsync()
    {
        if (SelectedNode?.Node is null)
        {
            ErrorMessage = "请先选择一个 PBS 节点";
            return;
        }
        if (SelectedNode.Node.ParentId is null)
        {
            ErrorMessage = "根节点没有同级节点，请使用“新建子节点”";
            return;
        }

        await OpenEditorAsync(null, SelectedNode.Node.ParentId);
    }

    [RelayCommand]
    private async Task EditNodeAsync()
    {
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
        var project = _context.CurrentProject ?? throw new ApiException("请先在顶部选择项目");
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

    private async Task OpenEditorAsync(PbsNodeDto? node, Guid? defaultParentId)
    {
        var project = _context.CurrentProject;
        if (project is null)
        {
            ErrorMessage = "请先在顶部选择项目";
            return;
        }

        var editorContext = new PbsNodeEditorContext(node, defaultParentId, ParentOptions.ToList());
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
                saved = await _session.Api.CreatePbsNodeAsync(project.Id, new CreatePbsNodeRequest(
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
        _ = RefreshAsync();
    }

    private void BuildTree(IReadOnlyList<PbsNodeDto> nodes, IReadOnlyList<EquipmentDto> equipments)
    {
        Roots.Clear();
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
                Roots.Add(item);
            }
        }

        var equipmentByNode = equipments
            .Where(x => x.PbsNodeId is not null)
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
