using CommunityToolkit.Mvvm.ComponentModel;
using WinDesign.Contracts.Telecom;

namespace WinDesign.DesignClient.ViewModels;

/// <summary>
/// 交换机选型与可用率。由「信息设计」页调整，两个页面共享同一份取值；
/// 「索引设计」页只读展示，不提供修改入口。
/// </summary>
public sealed partial class TelecomSwitchSettings : ObservableObject
{
    [ObservableProperty]
    private Guid? accessSwitchId;

    [ObservableProperty]
    private Guid? poeSwitchId;

    [ObservableProperty]
    private double utilizationPercent = 80;
}

/// <summary>按建筑物分别计算的交换机台数。</summary>
public sealed record BuildingSwitchRow(
    string Building,
    int DataPointCount,
    int ApCount,
    int AccessSwitchCount,
    int PoeSwitchCount);

/// <summary>
/// 交换机台数计算：按建筑物分组，先在楼内取整，再把各楼结果相加。
/// 信息设计与索引设计共用同一套算法，保证两个页面数字一致。
/// </summary>
public static class TelecomSwitchCalculator
{
    /// <summary>按可用端口数向上取整；电话点不参与计算。</summary>
    public static int Count(int pointCount, int ports, double utilizationPercent)
    {
        if (pointCount <= 0 || ports <= 0 || utilizationPercent <= 0)
        {
            return 0;
        }

        var usablePorts = ports * utilizationPercent / 100.0;
        return (int)Math.Ceiling(pointCount / usablePorts);
    }

    public static IReadOnlyList<BuildingSwitchRow> ByBuilding(
        IEnumerable<(Guid PbsNodeId, string Building, int DataPointCount, int ApCount)> rooms,
        int accessPorts,
        int poePorts,
        double utilizationPercent)
    {
        return rooms
            .GroupBy(x => x.PbsNodeId)
            .Select(g => new BuildingSwitchRow(
                string.IsNullOrWhiteSpace(g.First().Building) ? "未指定建筑" : g.First().Building,
                g.Sum(x => x.DataPointCount),
                g.Sum(x => x.ApCount),
                Count(g.Sum(x => x.DataPointCount), accessPorts, utilizationPercent),
                Count(g.Sum(x => x.ApCount), poePorts, utilizationPercent)))
            .OrderBy(x => x.Building, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static int TotalAccess(IEnumerable<BuildingSwitchRow> rows) =>
        rows.Sum(x => x.AccessSwitchCount);

    public static int TotalPoe(IEnumerable<BuildingSwitchRow> rows) =>
        rows.Sum(x => x.PoeSwitchCount);

    public static string DescribeSwitch(SwitchModelDto? model) =>
        model is null ? "未选择交换机样本" : $"{model.Code} · {model.ElectricalPorts} 口";
}
