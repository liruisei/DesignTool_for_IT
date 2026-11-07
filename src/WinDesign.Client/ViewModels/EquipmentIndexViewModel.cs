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
            saved = await _session.Api.CreateEquipmentAsync(project.Id, new CreateEquipmentRequest(
                EditPbsNodeId,
                EditTagNo.Trim(),
                EditName.Trim(),
                NullIfEmpty(EditTypeCode),
                EditDiscipline,
                EditStatus,
                NullIfEmpty(EditLocation),
                NullIfEmpty(EditDescription)));
            StatusMessage = "设备已创建";
        }
        else
        {
            if (SelectedEquipment is null)
            {
                throw new ApiException("请选择要编辑的设备");
            }

            saved = await _session.Api.UpdateEquipmentAsync(SelectedEquipment.Id, new UpdateEquipmentRequest(
                EditPbsNodeId,
                EditTagNo.Trim(),
                EditName.Trim(),
                NullIfEmpty(EditTypeCode),
                EditDiscipline,
                EditStatus,
                NullIfEmpty(EditLocation),
                NullIfEmpty(EditDescription)));
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
        EditStatus = value.Status;
        EditPbsNodeId = value.PbsNodeId;
        EditLocation = value.Location ?? string.Empty;
        EditDescription = value.Description ?? string.Empty;
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
