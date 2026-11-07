using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Contracts.Pbs;

namespace WinDesign.Client.ViewModels;

public partial class PbsNodeItem : ObservableObject
{
    public PbsNodeItem(PbsNodeDto node)
    {
        Node = node;
    }

    public PbsNodeDto Node { get; }
    public ObservableCollection<PbsNodeItem> Children { get; } = new();

    [ObservableProperty]
    private bool isExpanded = true;

    [ObservableProperty]
    private bool isSelected;

    public string Glyph => Node.NodeType switch
    {
        "Root" => "⌂",
        "Area" => "▦",
        "Unit" => "▣",
        "System" => "◇",
        "SubSystem" => "·",
        _ => "·"
    };
}

public sealed record PbsNodeOption(Guid? Id, string Display);

public sealed record PbsNodeTypeOption(string Code, string Name);

public partial class PbsViewModel : ViewModelBase
{
    private readonly SessionService _session;
    private readonly ProjectContext _context;
    private readonly IPbsDialogService _dialogs;

    public ObservableCollection<PbsNodeItem> Roots { get; } = new();
    public ObservableCollection<PbsNodeOption> ParentOptions { get; } = new();

    public IReadOnlyList<PbsNodeTypeOption> NodeTypeOptions { get; } = new[]
    {
        new PbsNodeTypeOption("Root", "根节点"),
        new PbsNodeTypeOption("Area", "区域"),
        new PbsNodeTypeOption("Unit", "装置"),
        new PbsNodeTypeOption("System", "系统"),
        new PbsNodeTypeOption("SubSystem", "子系统")
    };

    [ObservableProperty]
    private PbsNodeItem? selectedNode;

    [ObservableProperty]
    private string? statusMessage;

    public ProjectContext Context => _context;

    public PbsViewModel(SessionService session, ProjectContext context, IPbsDialogService? dialogs = null)
    {
        _session = session;
        _context = context;
        _dialogs = dialogs ?? new PbsDialogService();
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
            SelectedNode = null;
            return;
        }

        var nodes = await _session.Api.GetPbsNodesAsync(project.Id);
        BuildTree(nodes);
        BuildParentOptions();

        if (SelectedNode is not null)
        {
            SelectedNode = FindItem(Roots, SelectedNode.Node.Id);
        }
    });

    [RelayCommand]
    private Task NewRootAsync() => OpenEditorAsync(null, null);

    [RelayCommand]
    private async Task NewChildAsync()
    {
        if (SelectedNode is null)
        {
            ErrorMessage = "请先选择一个上级节点";
            return;
        }

        await OpenEditorAsync(null, SelectedNode.Node.Id);
    }

    [RelayCommand]
    private async Task NewSiblingAsync()
    {
        if (SelectedNode is null)
        {
            ErrorMessage = "请先选择一个节点";
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
        if (SelectedNode is null)
        {
            ErrorMessage = "请选择要编辑的节点";
            return;
        }

        await OpenEditorAsync(SelectedNode.Node, null);
    }

    [RelayCommand]
    private Task DeleteNodeAsync() => RunAsync(async () =>
    {
        if (SelectedNode is null)
        {
            throw new ApiException("请选择要删除的节点");
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
    private void ExpandAll() => SetExpanded(true);

    [RelayCommand]
    private void CollapseAll() => SetExpanded(false);

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
            SelectedNode = FindItem(Roots, saved.Id);
        });
    }

    private void OnProjectChanged(object? sender, EventArgs e)
    {
        _ = RefreshAsync();
    }

    private void BuildTree(IReadOnlyList<PbsNodeDto> nodes)
    {
        Roots.Clear();
        var map = nodes.ToDictionary(x => x.Id, x => new PbsNodeItem(x));
        foreach (var item in map.Values
                     .OrderBy(x => x.Node.SortOrder)
                     .ThenBy(x => x.Node.Code, StringComparer.OrdinalIgnoreCase))
        {
            if (item.Node.ParentId is Guid parentId && map.TryGetValue(parentId, out var parent))
            {
                parent.Children.Add(item);
            }
            else
            {
                Roots.Add(item);
            }
        }
    }

    private void BuildParentOptions()
    {
        ParentOptions.Clear();
        ParentOptions.Add(new PbsNodeOption(null, "（无 / 根节点）"));

        void Walk(IEnumerable<PbsNodeItem> items, int depth)
        {
            foreach (var item in items)
            {
                var indent = depth == 0 ? string.Empty : new string('·', depth * 2) + " ";
                ParentOptions.Add(new PbsNodeOption(item.Node.Id, indent + item.Node.Code + "  " + item.Node.Name));
                Walk(item.Children, depth + 1);
            }
        }

        Walk(Roots, 0);
    }

    private static PbsNodeItem? FindItem(IEnumerable<PbsNodeItem> items, Guid id)
    {
        foreach (var item in items)
        {
            if (item.Node.Id == id)
            {
                return item;
            }
            var found = FindItem(item.Children, id);
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
