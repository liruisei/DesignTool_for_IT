using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Telecom;

namespace WinDesign.DesignClient.ViewModels;

/// <summary>可编辑的电信房间行。新建行 Id 为空，保存成功后统一重新加载。</summary>
public partial class TelecomRoomRowViewModel : ObservableObject
{
    public TelecomRoomRowViewModel()
    {
    }

    public TelecomRoomRowViewModel(TelecomRoomDto dto)
    {
        Id = dto.Id;
        pbsNodeId = dto.PbsNodeId;
        floor = dto.Floor;
        code = dto.Code;
        name = dto.Name;
        dataPointCount = dto.DataPointCount;
        phonePointCount = dto.PhonePointCount;
        apCount = dto.ApCount;
        remark = dto.Remark;
        SortOrder = dto.SortOrder;
    }

    public Guid? Id { get; }
    public bool IsNew => Id is null;
    public int SortOrder { get; init; }

    [ObservableProperty]
    private Guid pbsNodeId;

    [ObservableProperty]
    private string floor = string.Empty;

    [ObservableProperty]
    private string code = string.Empty;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private int dataPointCount;

    [ObservableProperty]
    private int phonePointCount;

    [ObservableProperty]
    private int apCount;

    [ObservableProperty]
    private string? remark;

    /// <summary>是否存在未保存编辑；加载时直接写字段，不会误标脏。</summary>
    public bool IsDirty { get; private set; }

    public void MarkClean() => IsDirty = false;

    partial void OnPbsNodeIdChanged(Guid value) => IsDirty = true;
    partial void OnFloorChanged(string value) => IsDirty = true;
    partial void OnCodeChanged(string value) => IsDirty = true;
    partial void OnNameChanged(string value) => IsDirty = true;
    partial void OnDataPointCountChanged(int value) => IsDirty = true;
    partial void OnPhonePointCountChanged(int value) => IsDirty = true;
    partial void OnApCountChanged(int value) => IsDirty = true;
    partial void OnRemarkChanged(string? value) => IsDirty = true;
}

/// <summary>按建筑 / 楼层汇总的一行。</summary>
public sealed record TelecomSummaryRow(
    string Building,
    string Floor,
    string Room,
    int RoomCount,
    int DataPointCount,
    int PhonePointCount,
    int ApCount,
    int AccessSwitchCount,
    int PoeSwitchCount);

/// <summary>
/// 设计端电信统计：一页完成建筑/楼层/房间点位数录入、按建筑与楼层汇总，
/// 并按交换机样本库的端口规格和可用率实时计算接入 / PoE 交换机台数。
/// 电话点只统计、不占用交换机电口。
/// </summary>
public partial class TelecomStatsViewModel : ViewModelBase, IPendingChanges
{
    private readonly SessionService _session;
    private readonly ProjectContext _context;

    public ObservableCollection<TelecomRoomRowViewModel> Rooms { get; } = new();
    public ObservableCollection<PbsNodeOption> BuildingOptions { get; } = new();
    public ObservableCollection<SwitchModelDto> AccessSwitchModels { get; } = new();
    public ObservableCollection<SwitchModelDto> PoeSwitchModels { get; } = new();
    public ObservableCollection<TelecomSummaryRow> RoomSummaries { get; } = new();
    public ObservableCollection<TelecomSummaryRow> FloorSummaries { get; } = new();
    public ObservableCollection<TelecomSummaryRow> BuildingSummaries { get; } = new();

    [ObservableProperty]
    private TelecomRoomRowViewModel? selectedRoom;

    [ObservableProperty]
    private SwitchModelDto? selectedAccessSwitch;

    [ObservableProperty]
    private SwitchModelDto? selectedPoeSwitch;

    [ObservableProperty]
    private double utilizationPercent = 80;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private int buildingCount;

    [ObservableProperty]
    private int floorCount;

    [ObservableProperty]
    private int roomCount;

    [ObservableProperty]
    private int dataPointTotal;

    [ObservableProperty]
    private int phonePointTotal;

    [ObservableProperty]
    private int apTotal;

    [ObservableProperty]
    private int accessSwitchTotal;

    [ObservableProperty]
    private int poeSwitchTotal;

    public bool CanWrite => _session.IsAdmin || _session.IsDesigner;
    public bool IsReadOnly => !CanWrite;
    public ProjectContext Context => _context;

    public TelecomStatsViewModel(SessionService session, ProjectContext context)
    {
        _session = session;
        _context = context;
        Title = "电信统计";
        _context.ProjectChanged += OnProjectChanged;
        _ = RefreshAsync();
    }

    public override void Dispose() => _context.ProjectChanged -= OnProjectChanged;

    public override Task RefreshAsync() => RunAsync(async () =>
    {
        var project = _context.CurrentProject;
        var plantId = _session.PlantId;
        if (project is null || plantId is null)
        {
            ClearAll();
            return;
        }

        var nodes = await _session.Api.GetPbsNodesAsync(plantId.Value);
        BuildBuildingOptions(nodes, project.PbsRootNodeId);

        var models = await _session.Api.GetSwitchModelsAsync(activeOnly: true);
        var accessId = SelectedAccessSwitch?.Id;
        var poeId = SelectedPoeSwitch?.Id;
        AccessSwitchModels.Clear();
        PoeSwitchModels.Clear();
        foreach (var model in models)
        {
            if (string.Equals(model.SwitchType, "Access", StringComparison.OrdinalIgnoreCase))
            {
                AccessSwitchModels.Add(model);
            }
            else if (string.Equals(model.SwitchType, "PoE", StringComparison.OrdinalIgnoreCase))
            {
                PoeSwitchModels.Add(model);
            }
        }
        SelectedAccessSwitch = AccessSwitchModels.FirstOrDefault(x => x.Id == accessId)
                               ?? AccessSwitchModels.FirstOrDefault();
        SelectedPoeSwitch = PoeSwitchModels.FirstOrDefault(x => x.Id == poeId)
                            ?? PoeSwitchModels.FirstOrDefault();

        await LoadRoomsAsync(project.Id);
        StatusMessage = null;
    });

    [RelayCommand]
    private void AddRoom()
    {
        if (!CanWrite)
        {
            ErrorMessage = "当前账号没有编辑电信统计的权限";
            return;
        }
        if (BuildingOptions.Count == 0)
        {
            ErrorMessage = "当前项目 PBS 范围内没有建筑物节点，请先在设计端 PBS 中添加建筑物";
            return;
        }

        var row = new TelecomRoomRowViewModel
        {
            PbsNodeId = BuildingOptions[0].Id ?? Guid.Empty,
            Floor = "1F",
            Code = string.Empty,
            Name = string.Empty
        };
        row.PropertyChanged += OnRowPropertyChanged;
        Rooms.Add(row);
        SelectedRoom = row;
        StatusMessage = "已新增一行，填写后点「保存更改」";
    }

    [RelayCommand]
    private Task DeleteRoomAsync() => RunAsync(async () =>
    {
        if (!CanWrite)
        {
            throw new ApiException("当前账号没有编辑电信统计的权限");
        }
        if (SelectedRoom is not { } row)
        {
            throw new ApiException("请先选择要删除的房间");
        }

        if (!row.IsNew)
        {
            await _session.Api.DeleteTelecomRoomAsync(row.Id!.Value);
        }
        DetachRow(row);
        Rooms.Remove(row);
        SelectedRoom = null;
        Recompute();
        StatusMessage = "房间记录已删除";
    });

    /// <summary>整页保存：新建行 POST，已有行 PUT，成功后重新加载并使用服务端排序。</summary>
    [RelayCommand]
    private Task SaveChangesAsync() => RunAsync(async () =>
    {
        if (!CanWrite)
        {
            throw new ApiException("当前账号没有编辑电信统计的权限");
        }
        var project = _context.CurrentProject
            ?? throw new ApiException("请先选择工作区项目");

        ValidateRows(Rooms, requireAll: true);
        foreach (var row in Rooms.ToList())
        {
            var request = ToRequest(row);
            if (row.IsNew)
            {
                await _session.Api.CreateTelecomRoomAsync(project.Id, request);
            }
            else
            {
                await _session.Api.UpdateTelecomRoomAsync(row.Id!.Value, request);
            }
        }

        await LoadRoomsAsync(project.Id);
        StatusMessage = "电信统计已保存";
    });

    /// <summary>切换工作区前只提交已有房间的修改，新建未保存的行不自动创建。</summary>
    public async Task SavePendingAsync()
    {
        var dirty = Rooms.Where(x => !x.IsNew && x.IsDirty).ToList();
        if (dirty.Count == 0)
        {
            return;
        }

        ValidateRows(dirty, requireAll: true);
        foreach (var row in dirty)
        {
            await _session.Api.UpdateTelecomRoomAsync(row.Id!.Value, ToRequest(row));
        }
    }

    partial void OnUtilizationPercentChanged(double value) => Recompute();

    partial void OnSelectedAccessSwitchChanged(SwitchModelDto? value) => Recompute();

    partial void OnSelectedPoeSwitchChanged(SwitchModelDto? value) => Recompute();

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e) => Recompute();

    private void OnProjectChanged(object? sender, EventArgs e) => _ = RefreshAsync();

    private void ValidateRows(IReadOnlyCollection<TelecomRoomRowViewModel> rows, bool requireAll)
    {
        var buildingIds = BuildingOptions
            .Where(x => x.Id is not null)
            .Select(x => x.Id!.Value)
            .ToHashSet();

        var index = 0;
        foreach (var row in rows)
        {
            index++;
            if (!buildingIds.Contains(row.PbsNodeId))
            {
                throw new ApiException($"第 {index} 行：请选择当前项目 PBS 范围内的建筑物");
            }
            if (string.IsNullOrWhiteSpace(row.Floor))
            {
                throw new ApiException($"第 {index} 行：楼层不能为空");
            }
            if (requireAll && string.IsNullOrWhiteSpace(row.Code))
            {
                throw new ApiException($"第 {index} 行：房间编码不能为空");
            }
            // 房间名称允许为空，只保留编码作为必填标识。
            if (row.DataPointCount < 0 || row.PhonePointCount < 0 || row.ApCount < 0)
            {
                throw new ApiException($"第 {index} 行：点位数量不能为负数");
            }
        }
    }

    private static TelecomRoomUpsertRequest ToRequest(TelecomRoomRowViewModel row) => new(
        row.PbsNodeId,
        row.Floor.Trim(),
        row.Code.Trim(),
        row.Name.Trim(),
        row.DataPointCount,
        row.PhonePointCount,
        row.ApCount,
        string.IsNullOrWhiteSpace(row.Remark) ? null : row.Remark.Trim(),
        row.SortOrder);

    private async Task LoadRoomsAsync(Guid projectId)
    {
        foreach (var row in Rooms)
        {
            DetachRow(row);
        }
        Rooms.Clear();
        SelectedRoom = null;

        var rooms = await _session.Api.GetTelecomRoomsAsync(projectId);
        foreach (var dto in rooms)
        {
            var row = new TelecomRoomRowViewModel(dto);
            row.PropertyChanged += OnRowPropertyChanged;
            Rooms.Add(row);
        }

        Recompute();
    }

    private void BuildBuildingOptions(IReadOnlyList<PbsNodeDto> nodes, Guid? projectRootId)
    {
        BuildingOptions.Clear();

        var byId = nodes.ToDictionary(x => x.Id);
        bool InScope(PbsNodeDto node)
        {
            if (projectRootId is null)
            {
                return true;
            }

            var current = node;
            var visited = new HashSet<Guid>();
            while (visited.Add(current.Id))
            {
                if (current.Id == projectRootId)
                {
                    return true;
                }
                if (current.ParentId is not Guid parentId || !byId.TryGetValue(parentId, out var parent))
                {
                    return false;
                }
                current = parent;
            }
            return false;
        }

        var buildings = nodes
            .Where(x => string.Equals(x.NodeType, "Building", StringComparison.OrdinalIgnoreCase))
            .Where(InScope)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var building in buildings)
        {
            // 下拉与汇总只显示建筑物编号，名称/描述不展开。
            BuildingOptions.Add(new PbsNodeOption(building.Id, building.Code));
        }
    }

    private void Recompute()
    {
        var accessPorts = SelectedAccessSwitch?.ElectricalPorts ?? 0;
        var poePorts = SelectedPoeSwitch?.ElectricalPorts ?? 0;
        var utilization = Math.Clamp(UtilizationPercent, 1, 100);

        var buildingCodeById = BuildingOptions
            .Where(x => x.Id is not null)
            .ToDictionary(x => x.Id!.Value, x => x.Display);

        string BuildingCode(TelecomRoomRowViewModel row) =>
            buildingCodeById.TryGetValue(row.PbsNodeId, out var display) ? display : "未指定建筑";

        var roomGroups = Rooms
            .Select(x => new TelecomSummaryRow(
                BuildingCode(x),
                string.IsNullOrWhiteSpace(x.Floor) ? "未填写" : x.Floor.Trim(),
                (string.IsNullOrWhiteSpace(x.Code) ? string.Empty : x.Code + "  ") + x.Name,
                1,
                x.DataPointCount,
                x.PhonePointCount,
                x.ApCount,
                SwitchCount(x.DataPointCount, accessPorts, utilization),
                SwitchCount(x.ApCount, poePorts, utilization)))
            .OrderBy(x => x.Building, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Floor, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Room, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var floorGroups = Rooms
            .GroupBy(x => new { x.PbsNodeId, Floor = string.IsNullOrWhiteSpace(x.Floor) ? "未填写" : x.Floor.Trim() })
            .Select(g => new TelecomSummaryRow(
                BuildingCode(g.First()),
                g.Key.Floor,
                string.Empty,
                g.Count(),
                g.Sum(x => x.DataPointCount),
                g.Sum(x => x.PhonePointCount),
                g.Sum(x => x.ApCount),
                SwitchCount(g.Sum(x => x.DataPointCount), accessPorts, utilization),
                SwitchCount(g.Sum(x => x.ApCount), poePorts, utilization)))
            .OrderBy(x => x.Building, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Floor, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var buildingGroups = Rooms
            .GroupBy(x => x.PbsNodeId)
            .Select(g => new TelecomSummaryRow(
                BuildingCode(g.First()),
                "全部楼层",
                string.Empty,
                g.Count(),
                g.Sum(x => x.DataPointCount),
                g.Sum(x => x.PhonePointCount),
                g.Sum(x => x.ApCount),
                SwitchCount(g.Sum(x => x.DataPointCount), accessPorts, utilization),
                SwitchCount(g.Sum(x => x.ApCount), poePorts, utilization)))
            .OrderBy(x => x.Building, StringComparer.OrdinalIgnoreCase)
            .ToList();

        RoomSummaries.Clear();
        foreach (var row in roomGroups)
        {
            RoomSummaries.Add(row);
        }
        FloorSummaries.Clear();
        foreach (var row in floorGroups)
        {
            FloorSummaries.Add(row);
        }
        BuildingSummaries.Clear();
        foreach (var row in buildingGroups)
        {
            BuildingSummaries.Add(row);
        }

        BuildingCount = buildingGroups.Count;
        FloorCount = floorGroups.Count;
        RoomCount = Rooms.Count;
        DataPointTotal = Rooms.Sum(x => x.DataPointCount);
        PhonePointTotal = Rooms.Sum(x => x.PhonePointCount);
        ApTotal = Rooms.Sum(x => x.ApCount);
        AccessSwitchTotal = SwitchCount(DataPointTotal, accessPorts, utilization);
        PoeSwitchTotal = SwitchCount(ApTotal, poePorts, utilization);
    }

    /// <summary>按可用端口数向上取整；电话点不参与计算。</summary>
    private static int SwitchCount(int pointCount, int ports, double utilizationPercent)
    {
        if (pointCount <= 0 || ports <= 0 || utilizationPercent <= 0)
        {
            return 0;
        }

        var usablePorts = ports * utilizationPercent / 100.0;
        return (int)Math.Ceiling(pointCount / usablePorts);
    }

    private void ClearAll()
    {
        foreach (var row in Rooms)
        {
            DetachRow(row);
        }
        Rooms.Clear();
        BuildingOptions.Clear();
        AccessSwitchModels.Clear();
        PoeSwitchModels.Clear();
        RoomSummaries.Clear();
        FloorSummaries.Clear();
        BuildingSummaries.Clear();
        SelectedRoom = null;
        SelectedAccessSwitch = null;
        SelectedPoeSwitch = null;
        Recompute();
    }

    private void DetachRow(TelecomRoomRowViewModel row) =>
        row.PropertyChanged -= OnRowPropertyChanged;
}
