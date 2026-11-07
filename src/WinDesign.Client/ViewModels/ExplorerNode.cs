using System.Collections.ObjectModel;

namespace WinDesign.Client.ViewModels;

public sealed class ExplorerNode
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public string Glyph { get; init; } = "▪";
    public bool IsFolder { get; init; }
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
}
