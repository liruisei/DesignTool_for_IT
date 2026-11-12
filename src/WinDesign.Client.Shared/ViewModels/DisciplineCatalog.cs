namespace WinDesign.Client.ViewModels;

/// <summary>专业代码与显示名的统一目录，管理端与设计端共用。</summary>
public static class DisciplineCatalog
{
    public static IReadOnlyList<CodeNameOption> All { get; } = new[]
    {
        new CodeNameOption("Instrument", "仪表"),
        new CodeNameOption("Telecom", "电信"),
        new CodeNameOption("IT", "IT")
    };

    /// <summary>按代码取显示名；未知代码返回 null。</summary>
    public static string? Name(string? code) =>
        All.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))?.Name;

    /// <summary>显示名优先，未知代码原样返回，空值返回空字符串。</summary>
    public static string Display(string? code) => Name(code) ?? code ?? string.Empty;
}
