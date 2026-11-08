using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Pbs;

namespace WinDesign.Client.ViewModels;

public partial class EquipmentIndexViewModel : ViewModelBase
{
    private readonly SessionService _session;
    private readonly ProjectContext _context;
    private readonly IEquipmentDialogService _dialogs;

    public ObservableCollection<EquipmentDto> Equipments { get; } = new();
    public ObservableCollection<PbsNodeOption> PbsFilterOptions { get; } = new();
    public ObservableCollection<PbsNodeOption> PbsEditorOptions { get; } = new();
    public ObservableCollection<DictionaryItemDto> EquipmentTypes { get; } = new();

    public IReadOnlyList<CodeNameOption> FilterDisciplineOptions { get; } = new[]
    {
        new CodeNameOption("", "全部专业"),
        new CodeNameOption("Instrument", "仪表"),
        new CodeNameOption("Telecom", "电信"),
        new CodeNameOption("IT", "IT")
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
    private string? statusMessage;

    public ProjectContext Context => _context;

    public EquipmentIndexViewModel(SessionService session, ProjectContext context, IEquipmentDialogService? dialogs = null)
    {
        _session = session;
        _context = context;
        _dialogs = dialogs ?? new EquipmentDialogService();
        Title = "索引表";
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
            Equipments.Clear();
            PbsFilterOptions.Clear();
            PbsEditorOptions.Clear();
            EquipmentTypes.Clear();
            SelectedEquipment = null;
            return;
        }

        var nodes = await _session.Api.GetPbsNodesAsync(plantId.Value);
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
        var project = _context.CurrentProject ?? throw new ApiException("请先选择工作区项目");
        await LoadEquipmentsAsync(project.Id);
    });

    [RelayCommand]
    private Task NewEquipmentAsync() => OpenEditorAsync(null, false);

    [RelayCommand]
    private async Task EditEquipmentAsync()
    {
        if (SelectedEquipment is null)
        {
            ErrorMessage = "请先选择要编辑的设备";
            return;
        }

        await OpenEditorAsync(SelectedEquipment, false);
    }

    [RelayCommand]
    private async Task DuplicateEquipmentAsync()
    {
        if (SelectedEquipment is null)
        {
            ErrorMessage = "请先选择要复制的设备";
            return;
        }

        await OpenEditorAsync(SelectedEquipment, true);
    }

    [RelayCommand]
    private Task DeleteEquipmentAsync() => RunAsync(async () =>
    {
        if (SelectedEquipment is null)
        {
            throw new ApiException("请选择要删除的设备");
        }

        if (!_dialogs.Confirm($"确定删除设备 {SelectedEquipment.TagNo} 吗？"))
        {
            return;
        }

        await _session.Api.DeleteEquipmentAsync(SelectedEquipment.Id);
        StatusMessage = "设备已删除";
        SelectedEquipment = null;
        var project = _context.CurrentProject;
        if (project is not null)
        {
            await LoadEquipmentsAsync(project.Id);
        }
    });

    private async Task OpenEditorAsync(EquipmentDto? equipment, bool isCopy)
    {
        var project = _context.CurrentProject;
        if (project is null)
        {
            ErrorMessage = "请先选择工作区项目";
            return;
        }

        var context = new EquipmentEditorContext(
            equipment,
            isCopy,
            PbsEditorOptions.ToList(),
            EquipmentTypes.ToList());
        var request = _dialogs.EditEquipment(context);
        if (request is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            EquipmentDto saved;
            if (equipment is null || isCopy)
            {
                saved = await _session.Api.CreateEquipmentAsync(project.Id, request);
                StatusMessage = isCopy ? "设备已复制" : "设备已创建";
            }
            else
            {
                saved = await _session.Api.UpdateEquipmentAsync(equipment.Id, request);
                StatusMessage = "设备已保存";
            }

            await LoadEquipmentsAsync(project.Id);
            SelectedEquipment = Equipments.FirstOrDefault(x => x.Id == saved.Id);
        });
    }

    private void OnProjectChanged(object? sender, EventArgs e)
    {
        _ = RefreshAsync();
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
