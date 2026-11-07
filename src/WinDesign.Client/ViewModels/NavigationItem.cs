namespace WinDesign.Client.ViewModels;

public sealed class NavigationItem
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string Glyph { get; init; }
    public required Func<ViewModelBase> Factory { get; init; }
    public bool AdminOnly { get; init; }
}
