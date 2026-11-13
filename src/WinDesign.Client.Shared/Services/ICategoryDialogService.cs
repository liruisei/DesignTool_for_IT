using WinDesign.Client.ViewModels;

namespace WinDesign.Client.Services;

/// <summary>装置下「回路」「建筑物」分类节点的编辑弹窗。</summary>
public interface ICategoryDialogService
{
    CategoryEditResult? EditCategory(CategoryEditorContext context);
}
