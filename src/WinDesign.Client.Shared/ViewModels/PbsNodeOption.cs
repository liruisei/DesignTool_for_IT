namespace WinDesign.Client.ViewModels;

/// <summary>
/// PBS 下拉选项：Id 为 null 表示“全部 / 未分配”。
/// 管理端的 PBS 管理与设计端的索引表都会用到，因此放在共享库。
/// </summary>
public sealed record PbsNodeOption(Guid? Id, string Display);
