using System.Collections.ObjectModel;

namespace WinDesign.Client.ViewModels;

public sealed class ExplorerNode
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public string Glyph { get; init; } = "▪";
    /// <summary>图标字体；为空时使用控件默认字体（用于普通文本符号）。</summary>
    public string? GlyphFontFamily { get; init; }
    public bool IsFolder { get; init; }
    /// <summary>装置下的“设备”分类文件夹，右键可新增设备。</summary>
    public bool IsEquipmentFolder { get; init; }
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
