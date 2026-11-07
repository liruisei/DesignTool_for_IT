using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Pbs;

namespace WinDesign.Client.ViewModels;

public sealed record CodeNameOption(string Code, string Name);

public partial class EquipmentIndexViewModel : ViewModelBase
{
    private readonly SessionService _session;
    private readonly ProjectContext _context;

    public ObservableCollection<EquipmentDto> Equipments { get; } = new();
    public ObservableCollection<PbsNodeOption> PbsFilterOptions { get; } = new();
    public ObservableCollection<PbsNodeOption> PbsEditorOptions { get; } = new();
    public ObservableCollection<DictionaryItemDto> EquipmentTypes { get; } = new();

    public IReadOnlyList<CodeNameOption> DisciplineOptions { get; } = new[]
    {
        new CodeNameOption("Instrument", "仪表"),
        new CodeNameOption("Telecom", "电信"),
        new CodeNameOption("IT", "IT")
    };

    public IReadOnlyList<CodeNameOption> FilterDisciplineOptions { get; } = new[]
    {
        new CodeNameOption("", "全部专业"),
        new CodeNameOption("Instrument", "仪表"),
        new CodeNameOption("Telecom", "电信"),
        new CodeNameOption("IT", "IT")
    };

    public IReadOnlyList<CodeNameOption> StatusOptions { get; } = new[]
    {
        new CodeNameOption("Planned", "规划"),
        new CodeNameOption("Designed", "设计中"),
        new CodeNameOption("Approved", "已批准")
    };

    public IReadOnlyList<CodeNameOption> FilterStatusOptions { get; } = new[]
    {
        new CodeNameOption("", "全部状态"),
        new CodeNameOption("Planned", "规划"),
        new CodeNameOption("Designed", "设计中"),
        new CodeNameOption("Approved", "已批准")
    };

    [ObservableProperty]
    private EquipmentDto? selectedEquipment;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string filterDiscipline = string.Empty;

    [ObservableProperty]
    private string filterStatus = string.Empty;

    [ObservableProperty]
    private PbsNodeOption? filterPbsNode;

    [ObservableProperty]
    private string editorTitle = "新建设备";

    [ObservableProperty]
    private bool isCreating = true;

    [ObservableProperty]
    private string editTagNo = string.Empty;

    [ObservableProperty]
    private string editName = string.Empty;

    [ObservableProperty]
    private string? editTypeCode;

    [ObservableProperty]
    private string editDiscipline = "Instrument";

    [ObservableProperty]
    private string editStatus = "Planned";

    [ObservableProperty]
    private Guid? editPbsNodeId;

    [ObservableProperty]
    private string editLocation = string.Empty;

    [ObservableProperty]
    private string editDescription = string.Empty;

    [ObservableProperty]
    private string editLoopNo = string.Empty;

    [ObservableProperty]
    private int editQuantity = 1;

    [ObservableProperty]
    private string editPipeEquipmentNo = string.Empty;

    [ObservableProperty]
    private string editPurpose = string.Empty;

    [ObservableProperty]
    private string editPidDrawingNo = string.Empty;

    [ObservableProperty]
    private string editSpecNo = string.Empty;

    [ObservableProperty]
    private string editSpecPageNo = string.Empty;

    [ObservableProperty]
    private string editLoopDrawingNo = string.Empty;

    [ObservableProperty]
    private string editInstallDrawingNo = string.Empty;

    [ObservableProperty]
    private string editElectricalDrawingNo = string.Empty;

    [ObservableProperty]
    private string editHeatSupplyDrawingNo = string.Empty;

    [ObservableProperty]
    private string editAirSupplyDrawingNo = string.Empty;

    [ObservableProperty]
    private string editLayingDrawingNo = string.Empty;

    [ObservableProperty]
    private string editJunctionBoxDrawingNo = string.Empty;

    [ObservableProperty]
    private string editKeyword = string.Empty;

    [ObservableProperty]
    private string editIoSetting = string.Empty;

    [ObservableProperty]
    private string editIndexNo = string.Empty;

    [ObservableProperty]
    private int editSortOrder;

    [ObservableProperty]
    private string editProcessCondition = string.Empty;

    [ObservableProperty]
    private string editSpecification = string.Empty;

    [ObservableProperty]
    private string? statusMessage;

    public ProjectContext Context => _context;

    public EquipmentIndexViewModel(SessionService session, ProjectContext context)
    {
        _session = session;
        _context = context;
        Title = "设备索引";
        _context.ProjectChanged += OnProjectChanged;
        _ = RefreshAsync();
    }

    public override Task RefreshAsync() => RunAsync(async () =>
    {
        var project = _context.CurrentProject;
        if (project is null)
        {
            Equipments.Clear();
            PbsFilterOptions.Clear();
            PbsEditorOptions.Clear();
            EquipmentTypes.Clear();
            SelectedEquipment = null;
            return;
        }

        var nodes = await _session.Api.GetPbsNodesAsync(project.Id);
        BuildPbsOptions(nodes);

        var types = await _session.Api.GetDictionaryItemsAsync("EQUIPMENT_TYPE");
        EquipmentTypes.Clear();
        foreach (var type in types)
        {
            EquipmentTypes.Add(type);
        }

        FilterPbsNode ??= PbsFilterOptions.FirstOrDefault();
        await LoadEquipmentsAsync(project.Id);
    });

    [RelayCommand]
    private Task SearchAsync() => RunAsync(async () =>
    {
        var project = _context.CurrentProject ?? throw new ApiException("请先在顶部选择项目");
        await LoadEquipmentsAsync(project.Id);
    });

    [RelayCommand]
    private void NewEquipment()
    {
        SelectedEquipment = null;
        IsCreating = true;
        EditorTitle = "新建设备";
        EditTagNo = string.Empty;
        EditName = string.Empty;
        EditTypeCode = null;
        EditDiscipline = "Instrument";
        EditStatus = "Planned";
        EditPbsNodeId = FilterPbsNode?.Id;
        EditLocation = string.Empty;
        EditDescription = string.Empty;
        EditLoopNo = string.Empty;
        EditQuantity = 1;
        EditPipeEquipmentNo = string.Empty;
        EditPurpose = string.Empty;
        EditPidDrawingNo = string.Empty;
        EditSpecNo = string.Empty;
        EditSpecPageNo = string.Empty;
        EditLoopDrawingNo = string.Empty;
        EditInstallDrawingNo = string.Empty;
        EditElectricalDrawingNo = string.Empty;
        EditHeatSupplyDrawingNo = string.Empty;
        EditAirSupplyDrawingNo = string.Empty;
        EditLayingDrawingNo = string.Empty;
        EditJunctionBoxDrawingNo = string.Empty;
        EditKeyword = string.Empty;
        EditIoSetting = string.Empty;
        EditIndexNo = string.Empty;
        EditSortOrder = 0;
        EditProcessCondition = string.Empty;
        EditSpecification = string.Empty;
        StatusMessage = null;
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        var project = _context.CurrentProject ?? throw new ApiException("请先在顶部选择项目");
        if (string.IsNullOrWhiteSpace(EditTagNo) || string.IsNullOrWhiteSpace(EditName))
        {
            throw new ApiException("设备位号和名称不能为空");
        }

        EquipmentDto saved;
        if (IsCreating)
        {
            saved = await _session.Api.CreateEquipmentAsync(project.Id, new CreateEquipmentRequest
            {
                PbsNodeId = EditPbsNodeId,
                TagNo = EditTagNo.Trim(),
                LoopNo = NullIfEmpty(EditLoopNo),
                Name = EditName.Trim(),
                TypeCode = NullIfEmpty(EditTypeCode),
                Discipline = EditDiscipline,
                Status = EditStatus,
                Location = NullIfEmpty(EditLocation),
                PipeEquipmentNo = NullIfEmpty(EditPipeEquipmentNo),
                Quantity = EditQuantity,
                Purpose = NullIfEmpty(EditPurpose),
                PidDrawingNo = NullIfEmpty(EditPidDrawingNo),
                SpecNo = NullIfEmpty(EditSpecNo),
                SpecPageNo = NullIfEmpty(EditSpecPageNo),
                LoopDrawingNo = NullIfEmpty(EditLoopDrawingNo),
                InstallDrawingNo = NullIfEmpty(EditInstallDrawingNo),
                ElectricalDrawingNo = NullIfEmpty(EditElectricalDrawingNo),
                HeatSupplyDrawingNo = NullIfEmpty(EditHeatSupplyDrawingNo),
                AirSupplyDrawingNo = NullIfEmpty(EditAirSupplyDrawingNo),
                LayingDrawingNo = NullIfEmpty(EditLayingDrawingNo),
                JunctionBoxDrawingNo = NullIfEmpty(EditJunctionBoxDrawingNo),
                Keyword = NullIfEmpty(EditKeyword),
                IoSetting = NullIfEmpty(EditIoSetting),
                IndexNo = NullIfEmpty(EditIndexNo),
                SortOrder = EditSortOrder,
                Description = NullIfEmpty(EditDescription),
                ProcessCondition = NullIfEmpty(EditProcessCondition),
                Specification = NullIfEmpty(EditSpecification)
            });
            StatusMessage = "设备已创建";
        }
        else
        {
            if (SelectedEquipment is null)
            {
                throw new ApiException("请选择要编辑的设备");
            }

            saved = await _session.Api.UpdateEquipmentAsync(SelectedEquipment.Id, new UpdateEquipmentRequest
            {
                PbsNodeId = EditPbsNodeId,
                TagNo = EditTagNo.Trim(),
                LoopNo = NullIfEmpty(EditLoopNo),
                Name = EditName.Trim(),
                TypeCode = NullIfEmpty(EditTypeCode),
                Discipline = EditDiscipline,
                Status = EditStatus,
                Location = NullIfEmpty(EditLocation),
                PipeEquipmentNo = NullIfEmpty(EditPipeEquipmentNo),
                Quantity = EditQuantity,
                Purpose = NullIfEmpty(EditPurpose),
                PidDrawingNo = NullIfEmpty(EditPidDrawingNo),
                SpecNo = NullIfEmpty(EditSpecNo),
                SpecPageNo = NullIfEmpty(EditSpecPageNo),
                LoopDrawingNo = NullIfEmpty(EditLoopDrawingNo),
                InstallDrawingNo = NullIfEmpty(EditInstallDrawingNo),
                ElectricalDrawingNo = NullIfEmpty(EditElectricalDrawingNo),
                HeatSupplyDrawingNo = NullIfEmpty(EditHeatSupplyDrawingNo),
                AirSupplyDrawingNo = NullIfEmpty(EditAirSupplyDrawingNo),
                LayingDrawingNo = NullIfEmpty(EditLayingDrawingNo),
                JunctionBoxDrawingNo = NullIfEmpty(EditJunctionBoxDrawingNo),
                Keyword = NullIfEmpty(EditKeyword),
                IoSetting = NullIfEmpty(EditIoSetting),
                IndexNo = NullIfEmpty(EditIndexNo),
                SortOrder = EditSortOrder,
                Description = NullIfEmpty(EditDescription),
                ProcessCondition = NullIfEmpty(EditProcessCondition),
                Specification = NullIfEmpty(EditSpecification)
            });
            StatusMessage = "设备已保存";
        }

        await LoadEquipmentsAsync(project.Id);
        SelectedEquipment = Equipments.FirstOrDefault(x => x.Id == saved.Id);
    });

    [RelayCommand]
    private Task DeleteAsync() => RunAsync(async () =>
    {
        if (SelectedEquipment is null)
        {
            throw new ApiException("请选择要删除的设备");
        }

        await _session.Api.DeleteEquipmentAsync(SelectedEquipment.Id);
        StatusMessage = "设备已删除";
        SelectedEquipment = null;
        IsCreating = true;
        EditorTitle = "新建设备";
        var project = _context.CurrentProject;
        if (project is not null)
        {
            await LoadEquipmentsAsync(project.Id);
        }
    });

    private void OnProjectChanged(object? sender, EventArgs e)
    {
        _ = RefreshAsync();
    }

    partial void OnSelectedEquipmentChanged(EquipmentDto? value)
    {
        if (value is null)
        {
            return;
        }

        IsCreating = false;
        EditorTitle = "编辑设备：" + value.TagNo;
        EditTagNo = value.TagNo;
        EditName = value.Name;
        EditTypeCode = value.TypeCode;
        EditDiscipline = value.Discipline;
        EditStatus = value.Status ?? "Planned";
        EditPbsNodeId = value.PbsNodeId;
        EditLocation = value.Location ?? string.Empty;
        EditDescription = value.Description ?? string.Empty;
        EditLoopNo = value.LoopNo ?? string.Empty;
        EditQuantity = value.Quantity;
        EditPipeEquipmentNo = value.PipeEquipmentNo ?? string.Empty;
        EditPurpose = value.Purpose ?? string.Empty;
        EditPidDrawingNo = value.PidDrawingNo ?? string.Empty;
        EditSpecNo = value.SpecNo ?? string.Empty;
        EditSpecPageNo = value.SpecPageNo ?? string.Empty;
        EditLoopDrawingNo = value.LoopDrawingNo ?? string.Empty;
        EditInstallDrawingNo = value.InstallDrawingNo ?? string.Empty;
        EditElectricalDrawingNo = value.ElectricalDrawingNo ?? string.Empty;
        EditHeatSupplyDrawingNo = value.HeatSupplyDrawingNo ?? string.Empty;
        EditAirSupplyDrawingNo = value.AirSupplyDrawingNo ?? string.Empty;
        EditLayingDrawingNo = value.LayingDrawingNo ?? string.Empty;
        EditJunctionBoxDrawingNo = value.JunctionBoxDrawingNo ?? string.Empty;
        EditKeyword = value.Keyword ?? string.Empty;
        EditIoSetting = value.IoSetting ?? string.Empty;
        EditIndexNo = value.IndexNo ?? string.Empty;
        EditSortOrder = value.SortOrder;
        EditProcessCondition = value.ProcessCondition ?? string.Empty;
        EditSpecification = value.Specification ?? string.Empty;
        StatusMessage = null;
    }

    private async Task LoadEquipmentsAsync(Guid projectId)
    {
        var result = await _session.Api.GetEquipmentsAsync(
            projectId,
            SearchText,
            NullIfEmpty(FilterDiscipline),
            NullIfEmpty(FilterStatus),
            FilterPbsNode?.Id);

        var currentId = SelectedEquipment?.Id;
        Equipments.Clear();
        foreach (var equipment in result.Items)
        {
            Equipments.Add(equipment);
        }
        if (currentId is not null)
        {
            SelectedEquipment = Equipments.FirstOrDefault(x => x.Id == currentId);
        }
    }

    private void BuildPbsOptions(IReadOnlyList<PbsNodeDto> nodes)
    {
        PbsFilterOptions.Clear();
        PbsEditorOptions.Clear();
        PbsFilterOptions.Add(new PbsNodeOption(null, "全部 PBS"));
        PbsEditorOptions.Add(new PbsNodeOption(null, "（未分配）"));

        var children = new Dictionary<Guid, List<PbsNodeDto>>();
        foreach (var node in nodes)
        {
            var parentKey = node.ParentId ?? Guid.Empty;
            if (!children.TryGetValue(parentKey, out var list))
            {
                list = new List<PbsNodeDto>();
                children[parentKey] = list;
            }
            list.Add(node);
        }
        foreach (var list in children.Values)
        {
            list.Sort((a, b) =>
            {
                var order = a.SortOrder.CompareTo(b.SortOrder);
                return order != 0 ? order : string.Compare(a.Code, b.Code, StringComparison.OrdinalIgnoreCase);
            });
        }

        void Walk(Guid? parentId, int depth)
        {
            if (!children.TryGetValue(parentId ?? Guid.Empty, out var list))
            {
                return;
            }
            foreach (var node in list)
            {
                var indent = depth == 0 ? string.Empty : new string('·', depth * 2) + " ";
                var display = indent + node.Code + "  " + node.Name;
                PbsFilterOptions.Add(new PbsNodeOption(node.Id, display));
                PbsEditorOptions.Add(new PbsNodeOption(node.Id, display));
                Walk(node.Id, depth + 1);
            }
        }

        Walk(null, 0);
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
