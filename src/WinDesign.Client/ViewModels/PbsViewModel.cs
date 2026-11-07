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
}

public sealed record PbsNodeOption(Guid? Id, string Display);

public sealed record PbsNodeTypeOption(string Code, string Name);

public partial class PbsViewModel : ViewModelBase
{
    private readonly SessionService _session;
    private readonly ProjectContext _context;

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
    private PbsNodeOption? selectedParentOption;

    [ObservableProperty]
    private string editorTitle = "新建 PBS 节点";

    [ObservableProperty]
    private bool isCreating = true;

    [ObservableProperty]
    private string editCode = string.Empty;

    [ObservableProperty]
    private string editName = string.Empty;

    [ObservableProperty]
    private string editNodeType = "System";

    [ObservableProperty]
    private int editSortOrder;

    [ObservableProperty]
    private string editDescription = string.Empty;

    [ObservableProperty]
    private string? statusMessage;

    public ProjectContext Context => _context;

    public PbsViewModel(SessionService session, ProjectContext context)
    {
        _session = session;
        _context = context;
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
    private void NewNode()
    {
        var parentId = SelectedNode?.Node.Id;
        SelectedNode = null;
        IsCreating = true;
        EditorTitle = "新建 PBS 节点";
        EditCode = string.Empty;
        EditName = string.Empty;
        EditSortOrder = 0;
        EditDescription = string.Empty;
        EditNodeType = parentId is null ? "Root" : "System";
        SelectedParentOption = ParentOptions.FirstOrDefault(x => x.Id == parentId) ?? ParentOptions.FirstOrDefault();
        StatusMessage = null;
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        var project = _context.CurrentProject ?? throw new ApiException("请先在顶部选择项目");
        if (string.IsNullOrWhiteSpace(EditCode) || string.IsNullOrWhiteSpace(EditName))
        {
            throw new ApiException("节点编码和名称不能为空");
        }

        var parentId = SelectedParentOption?.Id;
        PbsNodeDto saved;
        if (IsCreating)
        {
            saved = await _session.Api.CreatePbsNodeAsync(project.Id, new CreatePbsNodeRequest(
                parentId,
                EditCode.Trim(),
                EditName.Trim(),
                EditNodeType,
                EditSortOrder,
                NullIfEmpty(EditDescription)));
            StatusMessage = "PBS 节点已创建";
        }
        else
        {
            if (SelectedNode is null)
            {
                throw new ApiException("请选择要编辑的节点");
            }

            saved = await _session.Api.UpdatePbsNodeAsync(SelectedNode.Node.Id, new UpdatePbsNodeRequest(
                parentId,
                EditCode.Trim(),
                EditName.Trim(),
                EditNodeType,
                EditSortOrder,
                NullIfEmpty(EditDescription)));
            StatusMessage = "PBS 节点已保存";
        }

        await RefreshAsync();
        SelectedNode = FindItem(Roots, saved.Id);
    });

    [RelayCommand]
    private Task DeleteAsync() => RunAsync(async () =>
    {
        if (SelectedNode is null)
        {
            throw new ApiException("请选择要删除的节点");
        }

        await _session.Api.DeletePbsNodeAsync(SelectedNode.Node.Id);
        StatusMessage = "PBS 节点已删除";
        SelectedNode = null;
        IsCreating = true;
        EditorTitle = "新建 PBS 节点";
        await RefreshAsync();
    });

    private void OnProjectChanged(object? sender, EventArgs e)
    {
        _ = RefreshAsync();
    }

    partial void OnSelectedNodeChanged(PbsNodeItem? value)
    {
        if (value is null)
        {
            return;
        }

        IsCreating = false;
        EditorTitle = "编辑 PBS 节点：" + value.Node.Code;
        EditCode = value.Node.Code;
        EditName = value.Node.Name;
        EditNodeType = value.Node.NodeType;
        EditSortOrder = value.Node.SortOrder;
        EditDescription = value.Node.Description ?? string.Empty;
        SelectedParentOption = ParentOptions.FirstOrDefault(x => x.Id == value.Node.ParentId)
                               ?? ParentOptions.FirstOrDefault();
        StatusMessage = null;
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

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
