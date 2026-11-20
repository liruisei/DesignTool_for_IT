using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Telecom;

namespace WinDesign.DesignClient.ViewModels;

/// <summary>
/// 索引设计：设备索引表 + 只读的交换机计算结果。
/// 选型与可用率来自「信息设计」页（共享 <see cref="TelecomSwitchSettings"/>），本页不提供修改入口。
/// </summary>
public partial class IndexDesignViewModel : ViewModelBase
{
    private readonly SessionService _session;
    private readonly ProjectContext _context;
    private readonly TelecomSwitchSettings _switchSettings;
    private IReadOnlyList<SwitchModelDto> _models = Array.Empty<SwitchModelDto>();
    private IReadOnlyList<TelecomRoomDto> _rooms = Array.Empty<TelecomRoomDto>();
    private IReadOnlyList<DictionaryItemDto> _equipmentTypes = Array.Empty<DictionaryItemDto>();
    private IReadOnlyList<EquipmentDto> _projectEquipments = Array.Empty<EquipmentDto>();

    public EquipmentIndexViewModel IndexTable { get; }

    public ObservableCollection<BuildingSwitchRow> BuildingSwitchTotals { get; } = new();

    [ObservableProperty]
    private string accessSwitchText = "未选择交换机样本";

    [ObservableProperty]
    private string poeSwitchText = "未选择交换机样本";

    [ObservableProperty]
    private string utilizationText = "80%";

    [ObservableProperty]
    private int accessSwitchTotal;

    [ObservableProperty]
    private int poeSwitchTotal;

    /// <summary>计算结果减去索引表中已建的接入交换机台数，可为负数（冗余）。</summary>
    [ObservableProperty]
    private int accessSwitchRemaining;

    /// <summary>计算结果减去索引表中已建的 PoE 交换机台数，可为负数（冗余）。</summary>
    [ObservableProperty]
    private int poeSwitchRemaining;

    public string AccessSwitchRemainingText => $"剩余 {AccessSwitchRemaining} 台";

    public string PoeSwitchRemainingText => $"剩余 {PoeSwitchRemaining} 台";

    public IndexDesignViewModel(
        SessionService session,
        ProjectContext context,
        TelecomSwitchSettings switchSettings)
    {
        _session = session;
        _context = context;
        _switchSettings = switchSettings;
        Title = "索引设计";
        IndexTable = new EquipmentIndexViewModel(session, context);
        IndexTable.EquipmentsChanged += OnEquipmentsChanged;
        _switchSettings.PropertyChanged += OnSwitchSettingsChanged;
        _context.ProjectChanged += OnProjectChanged;
        _ = RefreshAsync();
    }

    public override void Dispose()
    {
        _switchSettings.PropertyChanged -= OnSwitchSettingsChanged;
        _context.ProjectChanged -= OnProjectChanged;
        IndexTable.EquipmentsChanged -= OnEquipmentsChanged;
        IndexTable.Dispose();
    }

    public override Task RefreshAsync() => RunAsync(async () =>
    {
        var project = _context.CurrentProject;
        if (project is null)
        {
            _models = Array.Empty<SwitchModelDto>();
            _rooms = Array.Empty<TelecomRoomDto>();
            _equipmentTypes = Array.Empty<DictionaryItemDto>();
            _projectEquipments = Array.Empty<EquipmentDto>();
            Recompute();
            return;
        }

        _models = await _session.Api.GetSwitchModelsAsync(activeOnly: true);
        _rooms = await _session.Api.GetTelecomRoomsAsync(project.Id);
        await RefreshUsageSourceAsync(project.Id, includeTypes: true);
        Recompute();
    });

    private void OnSwitchSettingsChanged(object? sender, PropertyChangedEventArgs e) => Recompute();

    private void OnProjectChanged(object? sender, EventArgs e) => _ = RefreshAsync();

    /// <summary>索引表增删设备后，重新取已建交换机台数，右侧「剩余」随之更新。</summary>
    private void OnEquipmentsChanged(object? sender, EventArgs e) => _ = RefreshUsageAsync();

    private async Task RefreshUsageAsync()
    {
        var project = _context.CurrentProject;
        if (project is null)
        {
            _projectEquipments = Array.Empty<EquipmentDto>();
            UpdateSwitchUsage();
            return;
        }

        try
        {
            await RefreshUsageSourceAsync(project.Id, includeTypes: true);
        }
        catch
        {
            // 索引表自身会显示读取错误；这里保持上一次的统计结果。
        }

        UpdateSwitchUsage();
    }

    private async Task RefreshUsageSourceAsync(Guid projectId, bool includeTypes)
    {
        if (includeTypes)
        {
            _equipmentTypes = await _session.Api.GetDictionaryItemsAsync("EQUIPMENT_TYPE");
        }
        _projectEquipments = await LoadProjectEquipmentsAsync(projectId);
    }

    /// <summary>分页读取当前项目的全部设备，不受索引表当前筛选条件影响。</summary>
    private async Task<IReadOnlyList<EquipmentDto>> LoadProjectEquipmentsAsync(Guid projectId)
    {
        const int pageSize = 500;
        var all = new List<EquipmentDto>();
        var page = 1;
        while (true)
        {
            var result = await _session.Api.GetEquipmentsAsync(projectId, pageSize: pageSize);
            all.AddRange(result.Items);
            if (result.Items.Count < pageSize || all.Count >= result.Total)
            {
                break;
            }
            page++;
        }
        return all;
    }

    private void Recompute()
    {
        var access = Resolve("Access", _switchSettings.AccessSwitchId);
        var poe = Resolve("PoE", _switchSettings.PoeSwitchId);
        var utilization = Math.Clamp(_switchSettings.UtilizationPercent, 1, 100);

        AccessSwitchText = Describe(access);
        PoeSwitchText = Describe(poe);
        UtilizationText = $"{utilization:0.#}%";

        var rows = TelecomSwitchCalculator.ByBuilding(
            _rooms.Select(x => (x.PbsNodeId, x.BuildingCode, x.DataPointCount, x.ApCount)),
            access?.ElectricalPorts ?? 0,
            poe?.ElectricalPorts ?? 0,
            utilization);

        BuildingSwitchTotals.Clear();
        foreach (var row in rows)
        {
            BuildingSwitchTotals.Add(row);
        }

        AccessSwitchTotal = TelecomSwitchCalculator.TotalAccess(rows);
        PoeSwitchTotal = TelecomSwitchCalculator.TotalPoe(rows);
        UpdateSwitchUsage();
    }

    /// <summary>
    /// 已建设备按设备类型名称分类：名称含 PoE 的算 PoE 交换机，其余名称含「交换机」的算接入交换机。
    /// 剩余台数 = 计算结果 − 已建台数，允许负数表示人为增加冗余。
    /// </summary>
    private void UpdateSwitchUsage()
    {
        var typeNames = _equipmentTypes
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Name, StringComparer.OrdinalIgnoreCase);

        var createdAccess = 0;
        var createdPoe = 0;
        foreach (var equipment in _projectEquipments)
        {
            if (equipment.TypeCode is not { Length: > 0 } code ||
                !typeNames.TryGetValue(code, out var typeName))
            {
                continue;
            }

            if (typeName.Contains("PoE", StringComparison.OrdinalIgnoreCase))
            {
                createdPoe++;
            }
            else if (typeName.Contains("交换机", StringComparison.Ordinal))
            {
                createdAccess++;
            }
        }

        AccessSwitchRemaining = AccessSwitchTotal - createdAccess;
        PoeSwitchRemaining = PoeSwitchTotal - createdPoe;
    }

    partial void OnAccessSwitchRemainingChanged(int value) =>
        OnPropertyChanged(nameof(AccessSwitchRemainingText));

    partial void OnPoeSwitchRemainingChanged(int value) =>
        OnPropertyChanged(nameof(PoeSwitchRemainingText));

    private SwitchModelDto? Resolve(string switchType, Guid? id) =>
        _models.FirstOrDefault(x => x.Id == id)
        ?? _models.FirstOrDefault(x =>
            string.Equals(x.SwitchType, switchType, StringComparison.OrdinalIgnoreCase));

    private static string Describe(SwitchModelDto? model) =>
        model is null
            ? "未选择交换机样本"
            : $"{model.Code} · {model.Name}（{model.ElectricalPorts} 口）";
}
