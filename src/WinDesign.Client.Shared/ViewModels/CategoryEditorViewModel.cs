using CommunityToolkit.Mvvm.ComponentModel;

namespace WinDesign.Client.ViewModels;

/// <summary>
/// 装置下「回路」「建筑物」分类节点的编辑上下文。
/// <paramref name="UnitTitle"/> 用于在弹窗中提示该节点归属的装置。
/// </summary>
public sealed record CategoryEditorContext(
    ExplorerCategoryFolder Category,
    string UnitTitle,
    string? Code,
    string? Name,
    string? Description,
    bool IsReadOnly);

public sealed record CategoryEditResult(string Code, string Name, string? Description);

public partial class CategoryEditorViewModel : ObservableObject
{
    private readonly CategoryEditorContext _context;

    public CategoryEditorViewModel(CategoryEditorContext context)
    {
        _context = context;
        IsReadOnly = context.IsReadOnly;
        IsEditable = !context.IsReadOnly;
        CategoryName = context.Category == ExplorerCategoryFolder.Loop ? "回路" : "建筑物";
        Title = "新增" + CategoryName;
        Intro = context.UnitTitle.Length == 0
            ? $"在装置下新增{CategoryName}。"
            : $"在装置「{context.UnitTitle}」下新增{CategoryName}。";

        EditCode = context.Code ?? string.Empty;
        EditName = context.Name ?? string.Empty;
        EditDescription = context.Description ?? string.Empty;
    }

    public bool IsReadOnly { get; }
    public bool IsEditable { get; }
    public string CategoryName { get; }
    public string Title { get; }
    public string Intro { get; }

    [ObservableProperty]
    private string editCode = string.Empty;

    [ObservableProperty]
    private string editName = string.Empty;

    [ObservableProperty]
    private string editDescription = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    public bool TryBuild(out CategoryEditResult? result, out string? error)
    {
        result = null;
        error = null;

        if (IsReadOnly)
        {
            error = $"当前账号为只读，不能新增{CategoryName}";
            return false;
        }

        var code = EditCode.Trim();
        var name = EditName.Trim();
        if (code.Length == 0 || name.Length == 0)
        {
            error = $"{CategoryName}编号和名称不能为空";
            return false;
        }

        result = new CategoryEditResult(
            code,
            name,
            string.IsNullOrWhiteSpace(EditDescription) ? null : EditDescription.Trim());
        return true;
    }
}
