using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Telecom;

namespace WinDesign.DesignClient.ViewModels;

/// <summary>
/// 电信统计的 PBS 两级目录：装置 → 建筑物。
/// 建筑物的装置取 PBS 树中最近的上级 Unit；没有上级装置的建筑物归入「未分配装置」，
/// 保证任何建筑物都能在下拉中选到，而不是被静默过滤掉。
/// 下拉只显示编码，与设计端其他位置保持一致。
/// </summary>
public sealed class TelecomPbsCatalog
{
    public static readonly Guid UnassignedUnitId = Guid.Empty;

    private readonly Dictionary<Guid, List<PbsNodeOption>> _buildingsByUnit = new();
    private readonly Dictionary<Guid, Guid> _unitByBuilding = new();
    private readonly Dictionary<Guid, string> _buildingCodes = new();

    /// <summary>项目 PBS 范围内的装置；仅在存在无装置建筑物时追加「未分配装置」。</summary>
    public ObservableCollection<PbsNodeOption> Units { get; } = new();

    public IReadOnlyList<PbsNodeOption> BuildingsFor(Guid unitId) =>
        _buildingsByUnit.TryGetValue(unitId, out var list) ? list : Array.Empty<PbsNodeOption>();

    public Guid UnitOf(Guid buildingId) =>
        _unitByBuilding.TryGetValue(buildingId, out var unitId) ? unitId : UnassignedUnitId;

    public string? BuildingCode(Guid buildingId) =>
        _buildingCodes.TryGetValue(buildingId, out var code) ? code : null;

    public string UnitCode(Guid unitId) =>
        Units.FirstOrDefault(x => x.Id == unitId)?.Display ?? string.Empty;

    public bool ContainsBuilding(Guid buildingId) => _unitByBuilding.ContainsKey(buildingId);

    public bool ContainsUnit(Guid unitId) => Units.Any(x => x.Id == unitId);

    public void AddBuilding(Guid unitId, Guid buildingId, string code)
    {
        if (!_buildingsByUnit.TryGetValue(unitId, out var list))
        {
            list = new List<PbsNodeOption>();
            _buildingsByUnit[unitId] = list;
        }

        list.Add(new PbsNodeOption(buildingId, code));
        _unitByBuilding[buildingId] = unitId;
        _buildingCodes[buildingId] = code;
    }

    /// <summary>无装置祖先的建筑物在最后补一个「未分配装置」，避免它们无法选择。</summary>
    public void EnsureUnassignedUnit()
    {
        if (_buildingsByUnit.TryGetValue(UnassignedUnitId, out var list) && list.Count > 0)
        {
            Units.Add(new PbsNodeOption(UnassignedUnitId, "未分配装置"));
        }
    }

    /// <summary>
    /// 新增房间时的默认装置：优先登录工作区选择的装置，其次第一个有建筑物的装置。
    /// </summary>
    public Guid ResolveDefaultUnit(Guid? preferredUnitId)
    {
        if (preferredUnitId is Guid preferred &&
            _buildingsByUnit.TryGetValue(preferred, out var preferredList) &&
            preferredList.Count > 0)
        {
            return preferred;
        }

        var withBuildings = Units.FirstOrDefault(x => x.Id is Guid id && BuildingsFor(id).Count > 0);
        return withBuildings?.Id ?? Units.FirstOrDefault()?.Id ?? UnassignedUnitId;
    }
}

/// <summary>可编辑的电信房间行。新建行 Id 为空，保存成功后统一重新加载。</summary>
public partial class TelecomRoomRowViewModel : ObservableObject
{
    private readonly TelecomPbsCatalog? _catalog;
    private bool _initializing;
    private bool _syncingBuilding;
    private bool _syncingUnit;

    public TelecomRoomRowViewModel()
    {
    }

    public TelecomRoomRowViewModel(TelecomPbsCatalog catalog)
    {
        _catalog = catalog;
        foreach (var unit in catalog.Units)
        {
            UnitOptions.Add(unit);
        }
    }

    public TelecomRoomRowViewModel(TelecomRoomDto dto, TelecomPbsCatalog catalog) : this(catalog)
    {
        Id = dto.Id;
        floor = dto.Floor;
        code = dto.Code;
        name = dto.Name;
        dataPointCount = dto.DataPointCount;
        phonePointCount = dto.PhonePointCount;
        apCount = dto.ApCount;
        remark = dto.Remark;
        SortOrder = dto.SortOrder;

        // 加载已有记录：按建筑物反查装置并填充过滤后的建筑物下拉，不标记为脏。
        _initializing = true;
        UnitId = catalog.UnitOf(dto.PbsNodeId);
        RebuildBuildingOptions();
        PbsNodeId = dto.PbsNodeId;
        _initializing = false;
    }

    public Guid? Id { get; }
    public bool IsNew => Id is null;
    public int SortOrder { get; init; }

    /// <summary>装置编码：模板列本身没有可排序的值，用它作为 SortMemberPath 与筛选依据。</summary>
    public string UnitCode => _catalog?.UnitCode(UnitId) ?? string.Empty;

    /// <summary>建筑物编码：同上，供模板列排序和筛选。</summary>
    public string BuildingCode => _catalog?.BuildingCode(PbsNodeId) ?? string.Empty;

    /// <summary>装置下拉（仅编码），所有行共用同一份目录。</summary>
    public ObservableCollection<PbsNodeOption> UnitOptions { get; } = new();

    /// <summary>当前装置下的建筑物下拉（仅编码）。</summary>
    public ObservableCollection<PbsNodeOption> BuildingOptions { get; } = new();

    [ObservableProperty]
    private Guid unitId;

    [ObservableProperty]
    private Guid pbsNodeId;

    [ObservableProperty]
    private string floor = string.Empty;

    /// <summary>房间号：进入界面后作为房间的唯一标识，对应接口的 Code。</summary>
    [ObservableProperty]
    private string code = string.Empty;

    /// <summary>房间名称：接口兼容字段，当前页面不再单独展示。</summary>
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

    partial void OnPbsNodeIdChanged(Guid value)
    {
        if (_initializing)
        {
            return;
        }

        IsDirty = true;
        OnPropertyChanged(nameof(BuildingCode));
        if (_syncingBuilding || _catalog is null)
        {
            return;
        }

        // 直接改建筑物时同步装置，保证两列始终一致。
        var ownerUnitId = _catalog.UnitOf(value);
        if (ownerUnitId != UnitId && _catalog.ContainsUnit(ownerUnitId))
        {
            _syncingUnit = true;
            try
            {
                UnitId = ownerUnitId;
            }
            finally
            {
                _syncingUnit = false;
            }
        }
    }

    partial void OnUnitIdChanged(Guid value)
    {
        if (_initializing)
        {
            return;
        }

        OnPropertyChanged(nameof(UnitCode));
        if (_syncingUnit)
        {
            return;
        }

        IsDirty = true;
        RebuildBuildingOptions();
    }

    /// <summary>切换装置后只保留该装置下的建筑物；原建筑物不属于新装置时自动落到第一个。</summary>
    private void RebuildBuildingOptions()
    {
        if (_catalog is null)
        {
            return;
        }

        _syncingBuilding = true;
        try
        {
            BuildingOptions.Clear();
            foreach (var option in _catalog.BuildingsFor(UnitId))
            {
                BuildingOptions.Add(option);
            }

            if (!BuildingOptions.Any(x => x.Id == PbsNodeId))
            {
                // 当前建筑物不在新装置下时，自动选择该装置的第一个建筑物。
                PbsNodeId = BuildingOptions.FirstOrDefault()?.Id ?? Guid.Empty;
            }
        }
        finally
        {
            _syncingBuilding = false;
        }
    }

    partial void OnFloorChanged(string value) => IsDirty = true;
    partial void OnCodeChanged(string value) => IsDirty = true;
    partial void OnNameChanged(string value) => IsDirty = true;
    partial void OnDataPointCountChanged(int value) => IsDirty = true;
    partial void OnPhonePointCountChanged(int value) => IsDirty = true;
    partial void OnApCountChanged(int value) => IsDirty = true;
    partial void OnRemarkChanged(string? value) => IsDirty = true;
}

/// <summary>
/// 设计端信息设计：一页完成建筑/楼层/房间点位数录入，下方内嵌索引表，
/// 并按交换机样本库的端口规格和可用率实时计算接入 / PoE 交换机台数。
/// 交换机按建筑物独立配置：先在每栋建筑物内取整，再把各楼台数相加。
/// 电话点只统计、不占用交换机电口。
/// </summary>
public partial class TelecomStatsViewModel : ViewModelBase, IPendingChanges
{
    private readonly SessionService _session;
    private readonly ProjectContext _context;
    private readonly TelecomSwitchSettings _switchSettings;
    private readonly List<TelecomRoomRowViewModel> _selectedRooms = new();
    private TelecomPbsCatalog? _catalog;

    public ObservableCollection<TelecomRoomRowViewModel> Rooms { get; } = new();
    public ObservableCollection<PbsNodeOption> BuildingOptions { get; } = new();
    public ObservableCollection<SwitchModelDto> AccessSwitchModels { get; } = new();
    public ObservableCollection<SwitchModelDto> PoeSwitchModels { get; } = new();
    public ObservableCollection<BuildingSwitchRow> BuildingSwitchTotals { get; } = new();

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

    /// <summary>右键菜单文字：单行显示「删除」，多行显示「删除选中的 N 行」。</summary>
    [ObservableProperty]
    private string deleteMenuHeader = "删除";

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

    /// <summary>接入交换机结果下方的型号说明；未选样本时给出提示。</summary>
    public string AccessSwitchHint => TelecomSwitchCalculator.DescribeSwitch(SelectedAccessSwitch);

    /// <summary>PoE 交换机结果下方的型号说明；未选样本时给出提示。</summary>
    public string PoeSwitchHint => TelecomSwitchCalculator.DescribeSwitch(SelectedPoeSwitch);

    public bool CanWrite => _session.IsAdmin || _session.IsDesigner;
    public bool IsReadOnly => !CanWrite;
    public ProjectContext Context => _context;

    /// <summary>交换机选型与可用率；与索引设计页共享，本页是唯一可调整入口。</summary>
    public TelecomSwitchSettings SwitchSettings => _switchSettings;

    public TelecomStatsViewModel(
        SessionService session,
        ProjectContext context,
        TelecomSwitchSettings? switchSettings = null)
    {
        _session = session;
        _context = context;
        _switchSettings = switchSettings ?? new TelecomSwitchSettings();
        UtilizationPercent = _switchSettings.UtilizationPercent;
        Title = "信息设计";
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
        // 优先沿用当前页选择，其次使用共享设置（索引设计页展示的即这份取值）。
        var accessId = SelectedAccessSwitch?.Id ?? _switchSettings.AccessSwitchId;
        var poeId = SelectedPoeSwitch?.Id ?? _switchSettings.PoeSwitchId;
        AccessSwitchModels.Clear();
        PoeSwitchModels.Clear();
        foreach (var model in models)
        {
            // 类型不再区分 Access / PoE：两个下拉都列出全部启用样本，由用户各自选型。
            AccessSwitchModels.Add(model);
            PoeSwitchModels.Add(model);
        }

        SelectedAccessSwitch = AccessSwitchModels.FirstOrDefault(x => x.Id == accessId)
                               ?? AccessSwitchModels.FirstOrDefault(x => !x.IsPoe)
                               ?? AccessSwitchModels.FirstOrDefault();
        SelectedPoeSwitch = PoeSwitchModels.FirstOrDefault(x => x.Id == poeId)
                            ?? PoeSwitchModels.FirstOrDefault(x => x.IsPoe)
                            ?? PoeSwitchModels.FirstOrDefault();

        await LoadRoomsAsync(project.Id);
        StatusMessage = null;
    });

    [RelayCommand]
    private void AddRoom()
    {
        if (!CanWrite)
        {
            ErrorMessage = "当前账号没有编辑信息设计的权限";
            return;
        }
        if (_catalog is null || BuildingOptions.Count == 0)
        {
            ErrorMessage = "当前项目 PBS 范围内没有建筑物节点，请先在设计端 PBS 中添加建筑物";
            return;
        }

        // 默认带出登录工作区选中的装置；该装置下没有建筑物时回退到第一个可用的。
        var defaultUnitId = _catalog.ResolveDefaultUnit(_session.UnitId);
        var defaultBuilding = _catalog.BuildingsFor(defaultUnitId).FirstOrDefault();
        if (defaultBuilding is null)
        {
            ErrorMessage = "当前项目 PBS 范围内没有可选的建筑物，请先在设计端 PBS 中添加建筑物";
            return;
        }

        var row = new TelecomRoomRowViewModel(_catalog)
        {
            UnitId = defaultUnitId,
            PbsNodeId = defaultBuilding.Id ?? Guid.Empty,
            Floor = "1F",
            Code = string.Empty,
            Name = string.Empty
        };
        row.PropertyChanged += OnRowPropertyChanged;
        Rooms.Add(row);
        SelectedRoom = row;
        StatusMessage = "已新增一行，填写后点「保存更改」";
    }

    /// <summary>
    /// 行右键删除：右键的行在当前多选内时删除全部选中行，否则只删被右键的那一行。
    /// 不再提供常驻的批量删除按钮，避免误删。
    /// </summary>
    [RelayCommand]
    private Task DeleteRowAsync(TelecomRoomRowViewModel? row) => RunAsync(async () =>
    {
        if (!CanWrite)
        {
            throw new ApiException("当前账号没有编辑信息设计的权限");
        }
        if (row is null)
        {
            throw new ApiException("请先选择要删除的房间");
        }

        var targets = _selectedRooms.Count > 1 && _selectedRooms.Contains(row)
            ? _selectedRooms.ToList()
            : new List<TelecomRoomRowViewModel> { row };

        foreach (var target in targets)
        {
            if (!target.IsNew)
            {
                await _session.Api.DeleteTelecomRoomAsync(target.Id!.Value);
            }
            DetachRow(target);
            Rooms.Remove(target);
        }

        _selectedRooms.Clear();
        SelectedRoom = null;
        Recompute();
        StatusMessage = targets.Count == 1
            ? "房间记录已删除"
            : $"已删除 {targets.Count} 条房间记录";
    });

    /// <summary>由视图在 DataGrid 选择变化时回填，供右键批量删除使用。</summary>
    public void SetSelection(IEnumerable<TelecomRoomRowViewModel> rows)
    {
        _selectedRooms.Clear();
        _selectedRooms.AddRange(rows.Distinct());
        DeleteMenuHeader = _selectedRooms.Count > 1
            ? $"删除选中的 {_selectedRooms.Count} 行"
            : "删除";
    }

    /// <summary>整页保存：新建行 POST，已有行 PUT，成功后重新加载并使用服务端排序。</summary>
    [RelayCommand]
    private Task SaveChangesAsync() => RunAsync(async () =>
    {
        if (!CanWrite)
        {
            throw new ApiException("当前账号没有编辑信息设计的权限");
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
        StatusMessage = "信息设计已保存";
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

    partial void OnUtilizationPercentChanged(double value)
    {
        _switchSettings.UtilizationPercent = value;
        Recompute();
    }

    partial void OnSelectedAccessSwitchChanged(SwitchModelDto? value)
    {
        _switchSettings.AccessSwitchId = value?.Id;
        Recompute();
    }

    partial void OnSelectedPoeSwitchChanged(SwitchModelDto? value)
    {
        _switchSettings.PoeSwitchId = value?.Id;
        Recompute();
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e) => Recompute();

    private void OnProjectChanged(object? sender, EventArgs e) => _ = RefreshAsync();

    private void ValidateRows(IReadOnlyCollection<TelecomRoomRowViewModel> rows, bool requireAll)
    {
        var catalog = _catalog
            ?? throw new ApiException("PBS 目录尚未加载，请先刷新页面");

        var index = 0;
        foreach (var row in rows)
        {
            index++;
            if (catalog.Units.Count > 0 && !catalog.ContainsUnit(row.UnitId))
            {
                throw new ApiException($"第 {index} 行：请选择装置");
            }
            if (!catalog.ContainsBuilding(row.PbsNodeId))
            {
                throw new ApiException($"第 {index} 行：请选择当前项目 PBS 范围内的建筑物");
            }
            if (catalog.UnitOf(row.PbsNodeId) != row.UnitId)
            {
                throw new ApiException($"第 {index} 行：建筑物不属于所选装置，请重新选择");
            }
            if (string.IsNullOrWhiteSpace(row.Floor))
            {
                throw new ApiException($"第 {index} 行：楼层不能为空");
            }
            if (requireAll && string.IsNullOrWhiteSpace(row.Code))
            {
                throw new ApiException($"第 {index} 行：房间号不能为空");
            }
            // 房间名称是接口兼容字段，界面统一以房间号（Code）作为标识。
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
        _selectedRooms.Clear();
        SelectedRoom = null;
        DeleteMenuHeader = "删除";

        var catalog = _catalog ?? new TelecomPbsCatalog();
        var rooms = await _session.Api.GetTelecomRoomsAsync(projectId);
        foreach (var dto in rooms)
        {
            var row = new TelecomRoomRowViewModel(dto, catalog);
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

        var catalog = new TelecomPbsCatalog();

        // 装置统一按排序号、编码排列，下拉只显示编码。
        var units = nodes
            .Where(x => string.Equals(x.NodeType, "Unit", StringComparison.OrdinalIgnoreCase))
            .Where(InScope)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var unit in units)
        {
            catalog.Units.Add(new PbsNodeOption(unit.Id, unit.Code));
        }

        // 建筑物按「排序号 → 编码」排列，并归到最近的上级装置下。
        var buildings = nodes
            .Where(x => string.Equals(x.NodeType, "Building", StringComparison.OrdinalIgnoreCase))
            .Where(InScope)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var building in buildings)
        {
            catalog.AddBuilding(ResolveUnitId(building), building.Id, building.Code);
        }
        catalog.EnsureUnassignedUnit();

        _catalog = catalog;

        // 汇总与兼容用的扁平列表：按装置分组，避免同一工厂不同装置的建筑物交错。
        foreach (var unit in catalog.Units)
        {
            if (unit.Id is not Guid unitId)
            {
                continue;
            }
            foreach (var building in catalog.BuildingsFor(unitId))
            {
                BuildingOptions.Add(building);
            }
        }

        Guid ResolveUnitId(PbsNodeDto building)
        {
            var current = building;
            var visited = new HashSet<Guid>();
            while (visited.Add(current.Id))
            {
                if (string.Equals(current.NodeType, "Unit", StringComparison.OrdinalIgnoreCase))
                {
                    return current.Id;
                }
                if (current.ParentId is not Guid parentId || !byId.TryGetValue(parentId, out var parent))
                {
                    break;
                }
                current = parent;
            }
            return TelecomPbsCatalog.UnassignedUnitId;
        }
    }

    private void Recompute()
    {
        var accessPorts = SelectedAccessSwitch?.ElectricalPorts ?? 0;
        var poePorts = SelectedPoeSwitch?.ElectricalPorts ?? 0;
        var utilization = Math.Clamp(UtilizationPercent, 1, 100);

        BuildingCount = Rooms.Select(x => x.PbsNodeId).Distinct().Count();
        FloorCount = Rooms
            .Select(x => (x.PbsNodeId, Floor: string.IsNullOrWhiteSpace(x.Floor) ? "未填写" : x.Floor.Trim()))
            .Distinct()
            .Count();
        RoomCount = Rooms.Count;
        DataPointTotal = Rooms.Sum(x => x.DataPointCount);
        PhonePointTotal = Rooms.Sum(x => x.PhonePointCount);
        ApTotal = Rooms.Sum(x => x.ApCount);

        var buildingCodes = BuildingOptions
            .Where(x => x.Id is not null)
            .ToDictionary(x => x.Id!.Value, x => x.Display);

        string BuildingCode(TelecomRoomRowViewModel row) =>
            buildingCodes.TryGetValue(row.PbsNodeId, out var display) ? display : "未指定建筑";

        // 交换机按建筑物独立配置：每栋楼先各自向上取整，再把各楼台数相加。
        var buildingRows = TelecomSwitchCalculator.ByBuilding(
            Rooms.Select(x => (x.PbsNodeId, BuildingCode(x), x.DataPointCount, x.ApCount)),
            accessPorts,
            poePorts,
            utilization);

        BuildingSwitchTotals.Clear();
        foreach (var row in buildingRows)
        {
            BuildingSwitchTotals.Add(row);
        }

        AccessSwitchTotal = TelecomSwitchCalculator.TotalAccess(buildingRows);
        PoeSwitchTotal = TelecomSwitchCalculator.TotalPoe(buildingRows);

        OnPropertyChanged(nameof(AccessSwitchHint));
        OnPropertyChanged(nameof(PoeSwitchHint));
    }

    private void ClearAll()
    {
        foreach (var row in Rooms)
        {
            DetachRow(row);
        }
        Rooms.Clear();
        _selectedRooms.Clear();
        DeleteMenuHeader = "删除";
        BuildingOptions.Clear();
        _catalog = null;
        AccessSwitchModels.Clear();
        PoeSwitchModels.Clear();
        BuildingSwitchTotals.Clear();
        SelectedRoom = null;
        SelectedAccessSwitch = null;
        SelectedPoeSwitch = null;
        Recompute();
    }

    private void DetachRow(TelecomRoomRowViewModel row) =>
        row.PropertyChanged -= OnRowPropertyChanged;
}
