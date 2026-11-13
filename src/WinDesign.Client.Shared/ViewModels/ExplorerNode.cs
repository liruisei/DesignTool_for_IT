using System.Collections.ObjectModel;

namespace WinDesign.Client.ViewModels;

/// <summary>装置下固定分类文件夹的类型，决定右键「新增」的分派。</summary>
public enum ExplorerCategoryFolder
{
    None = 0,
    Equipment,
    Loop,
    Building
}

public sealed class ExplorerNode
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public string Glyph { get; init; } = "▪";
    /// <summary>图标字体；为空时使用控件默认字体（用于普通文本符号）。</summary>
    public string? GlyphFontFamily { get; init; }
    public bool IsFolder { get; init; }
    /// <summary>装置下的「设备」「回路」「建筑物」分类文件夹；None 表示普通节点。</summary>
    public ExplorerCategoryFolder CategoryFolder { get; init; }
    public bool IsCategoryFolder => CategoryFolder != ExplorerCategoryFolder.None;
    public bool IsEquipmentFolder => CategoryFolder == ExplorerCategoryFolder.Equipment;
    /// <summary>分类文件夹关联的装置 PBS 节点 Id。</summary>
    public Guid? RelatedPbsNodeId { get; init; }
    public bool IsExpanded { get; init; } = true;
    public Action? Open { get; init; }
    public ObservableCollection<ExplorerNode> Children { get; } = new();
}

public sealed class DocumentTab
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required ViewModelBase ViewModel { get; init; }
    public bool IsClosable { get; init; } = true;
    public bool HasBeenActivated { get; set; }
}
