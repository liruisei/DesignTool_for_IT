using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Pbs;

namespace WinDesign.Client.Tests;

public class PbsNodeEditorViewModelTests
{
    [Fact]
    public void NewRoot_DefaultsToRootType()
    {
        var options = CreateOptions(out _);

        var viewModel = new PbsNodeEditorViewModel(new PbsNodeEditorContext(null, null, options));

        Assert.True(viewModel.IsRoot);
        Assert.False(viewModel.CanChooseNodeType);
        Assert.Equal("Root", viewModel.EditNodeType);
        Assert.Null(viewModel.SelectedParentOption!.Id);
    }

    [Fact]
    public void NewChild_DefaultsToSystemAndParent()
    {
        var options = CreateOptions(out var parentId);

        var viewModel = new PbsNodeEditorViewModel(new PbsNodeEditorContext(null, parentId, options));

        Assert.False(viewModel.IsRoot);
        Assert.True(viewModel.CanChooseNodeType);
        Assert.Equal("System", viewModel.EditNodeType);
        Assert.Equal(parentId, viewModel.SelectedParentOption!.Id);
    }

    [Fact]
    public void TryBuild_EmptyCode_ReturnsError()
    {
        var options = CreateOptions(out var parentId);
        var viewModel = new PbsNodeEditorViewModel(new PbsNodeEditorContext(null, parentId, options))
        {
            EditCode = string.Empty,
            EditName = "节点"
        };

        var ok = viewModel.TryBuild(out _, out var error);

        Assert.False(ok);
        Assert.Contains("不能为空", error);
    }

    [Fact]
    public void TryBuild_WithParentAndRootType_ForcesSystem()
    {
        var options = CreateOptions(out var parentId);
        var viewModel = new PbsNodeEditorViewModel(new PbsNodeEditorContext(null, parentId, options))
        {
            EditCode = "N1",
            EditName = "节点",
            EditNodeType = "Root"
        };

        var ok = viewModel.TryBuild(out var result, out _);

        Assert.True(ok);
        Assert.Equal("System", result!.NodeType);
    }

    [Fact]
    public void EditExisting_PopulatesFields()
    {
        var options = CreateOptions(out var parentId);
        var node = new PbsNodeDto(Guid.NewGuid(), Guid.NewGuid(), parentId, "N1", "节点", "System", 3, "说明", 0, 0);

        var viewModel = new PbsNodeEditorViewModel(new PbsNodeEditorContext(node, null, options));

        Assert.Equal("编辑 PBS 节点", viewModel.Title);
        Assert.Equal("N1", viewModel.EditCode);
        Assert.Equal("节点", viewModel.EditName);
        Assert.Equal(3, viewModel.EditSortOrder);
        Assert.Equal("说明", viewModel.EditDescription);
        Assert.Equal(parentId, viewModel.SelectedParentOption!.Id);
    }

    private static IReadOnlyList<PbsNodeOption> CreateOptions(out Guid parentId)
    {
        parentId = Guid.NewGuid();
        return new[]
        {
            new PbsNodeOption(null, "（无 / 根节点）"),
            new PbsNodeOption(parentId, "ROOT  全厂")
        };
    }
}
