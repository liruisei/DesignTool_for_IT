using WinDesign.Client.ViewModels;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WinDesign.Contracts.Pbs;

namespace WinDesign.AdminClient.ViewModels;

public sealed record PbsNodeEditorContext(
    PbsNodeDto? Node,
    Guid? DefaultParentId,
    IReadOnlyList<PbsNodeOption> ParentOptions,
    string? DefaultNodeType = null);

public sealed record PbsNodeEditorResult(
    Guid? ParentId,
    string Code,
    string Name,
    string NodeType,
    int SortOrder,
    string? Description);

public partial class PbsNodeEditorViewModel : ObservableObject
{
    public IReadOnlyList<PbsNodeTypeOption> NodeTypeOptions { get; } = new[]
    {
        new PbsNodeTypeOption("Area", "区域"),
        new PbsNodeTypeOption("Unit", "装置"),
        new PbsNodeTypeOption("System", "系统"),
        new PbsNodeTypeOption("SubSystem", "子系统")
    };

    public ObservableCollection<PbsNodeOption> ParentOptions { get; } = new();

    [ObservableProperty]
    private string title = "PBS 节点";

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
    private PbsNodeOption? selectedParentOption;

    [ObservableProperty]
    private bool isRoot;

    [ObservableProperty]
    private bool canChooseNodeType = true;

    [ObservableProperty]
    private string? errorMessage;

    public PbsNodeEditorViewModel(PbsNodeEditorContext context)
    {
        foreach (var option in context.ParentOptions)
        {
            ParentOptions.Add(option);
        }

        if (context.Node is null)
        {
            Title = context.DefaultParentId is null ? "新建顶层节点" : "新建子节点";
            SelectedParentOption = ParentOptions.FirstOrDefault(x => x.Id == context.DefaultParentId)
                                   ?? ParentOptions.FirstOrDefault();
            EditNodeType = context.DefaultNodeType
                           ?? (context.DefaultParentId is null ? "Area" : "System");
        }
        else
        {
            Title = "编辑 PBS 节点";
            EditCode = context.Node.Code;
            EditName = context.Node.Name;
            EditNodeType = context.Node.NodeType;
            EditSortOrder = context.Node.SortOrder;
            EditDescription = context.Node.Description ?? string.Empty;
            SelectedParentOption = ParentOptions.FirstOrDefault(x => x.Id == context.Node.ParentId)
                                   ?? ParentOptions.FirstOrDefault();
        }

        UpdateRootState();
    }

    public bool TryBuild(out PbsNodeEditorResult? result, out string? error)
    {
        result = null;
        error = null;

        if (string.IsNullOrWhiteSpace(EditCode) || string.IsNullOrWhiteSpace(EditName))
        {
            error = "节点编码和名称不能为空";
            return false;
        }

        var parentId = SelectedParentOption?.Id;
        var nodeType = string.IsNullOrWhiteSpace(EditNodeType) ? "Area" : EditNodeType;
        // 工厂本身就是 PBS 根节点，历史数据中的 Root 类型要归一化为具体层级。
        if (nodeType == "Root")
        {
            nodeType = parentId is null ? "Area" : "System";
        }

        result = new PbsNodeEditorResult(
            parentId,
            EditCode.Trim(),
            EditName.Trim(),
            nodeType,
            EditSortOrder,
            string.IsNullOrWhiteSpace(EditDescription) ? null : EditDescription.Trim());
        return true;
    }

    partial void OnSelectedParentOptionChanged(PbsNodeOption? value)
    {
        UpdateRootState();
    }

    private void UpdateRootState()
    {
        IsRoot = SelectedParentOption?.Id is null;
        // 工厂本身就是根节点，顶层节点也需要明确层级（区域/装置/系统等）。
        CanChooseNodeType = true;
        if (string.IsNullOrWhiteSpace(EditNodeType) || EditNodeType == "Root")
        {
            EditNodeType = IsRoot ? "Area" : "System";
        }
    }
}
