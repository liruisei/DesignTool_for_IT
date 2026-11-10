using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Materials;

namespace WinDesign.DesignClient.ViewModels;

/// <summary>
/// 独立材料表页：列出当前项目范围内的材料/附件，支持按设备/关键字过滤。
/// Viewer 只读；Admin/Designer 可维护，服务端仍是最终授权点。
/// </summary>
public partial class MaterialIndexViewModel : ViewModelBase
{
    private readonly SessionService _session;
    private readonly ProjectContext _context;
    private readonly IMaterialDialogService _dialogs;
    private List<EquipmentDto> _equipment = new();

    public ObservableCollection<MaterialItemDto> Materials { get; } = new();
    public ObservableCollection<MaterialEquipmentOption> EquipmentFilterOptions { get; } = new();

    [ObservableProperty]
    private MaterialItemDto? selectedMaterial;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private MaterialEquipmentOption? filterEquipment;

    [ObservableProperty]
    private string? statusMessage;

    public ProjectContext Context => _context;

    public bool CanWrite => _session.IsAdmin || _session.IsDesigner;

    public bool IsReadOnly => !CanWrite;

    public MaterialIndexViewModel(
        SessionService session,
        ProjectContext context,
        IMaterialDialogService? dialogs = null)
    {
        _session = session;
        _context = context;
        _dialogs = dialogs ?? new MaterialDialogService();
        Title = "材料表";
        _context.ProjectChanged += OnProjectChanged;
        _ = RefreshAsync();
    }

    public override void Dispose() => _context.ProjectChanged -= OnProjectChanged;

    public override Task RefreshAsync() => RunAsync(async () =>
    {
        var project = _context.CurrentProject;
        if (project is null || _session.PlantId is null)
        {
            Clear();
            return;
        }

        // 材料与设备各自独立加载：设备接口失败时，材料列表仍应可用。
        await Task.WhenAll(LoadEquipmentAsync(project.Id), LoadMaterialsAsync(project.Id));
    });

    [RelayCommand]
    private Task SearchAsync() => RunAsync(async () =>
    {
        var project = _context.CurrentProject ?? throw new ApiException("请先选择工作区项目");
        await LoadMaterialsAsync(project.Id);
    });

    [RelayCommand]
    private Task NewMaterialAsync() => RunAsync(async () =>
    {
        var project = _context.CurrentProject ?? throw new ApiException("请先选择工作区项目");
        if (!CanWrite)
        {
            ErrorMessage = "当前账号没有新建材料的权限";
            return;
        }

        var context = new MaterialEditorContext(
            null,
            BuildEditorEquipmentOptions(),
            IsReadOnly: false);
        var result = _dialogs.EditMaterial(context);
        if (result is null)
        {
            return;
        }

        var saved = await _session.Api.CreateMaterialAsync(project.Id, ToRequest(result));
        StatusMessage = "材料已创建";
        await ReloadAndSelectAsync(project.Id, saved.Id);
    });

    [RelayCommand]
    private async Task OpenMaterialAsync()
    {
        if (SelectedMaterial is null)
        {
            ErrorMessage = "请先选择要打开的材料";
            return;
        }

        var project = _context.CurrentProject;
        if (project is null)
        {
            ErrorMessage = "请先选择工作区项目";
            return;
        }

        var selected = SelectedMaterial;
        var context = new MaterialEditorContext(
            selected,
            BuildEditorEquipmentOptions(),
            IsReadOnly: IsReadOnly);
        var result = _dialogs.EditMaterial(context);
        if (result is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var saved = await _session.Api.UpdateMaterialAsync(selected.Id, ToRequest(result));
            StatusMessage = "材料已保存";
            await ReloadAndSelectAsync(project.Id, saved.Id);
        });
    }

    [RelayCommand]
    private Task DeleteMaterialAsync() => RunAsync(async () =>
    {
        var project = _context.CurrentProject ?? throw new ApiException("请先选择工作区项目");
        if (!CanWrite)
        {
            ErrorMessage = "当前账号没有删除材料的权限";
            return;
        }
        if (SelectedMaterial is null)
        {
            ErrorMessage = "请选择要删除的材料";
            return;
        }

        if (!_dialogs.Confirm($"确定删除材料 {SelectedMaterial.Code} 吗？此操作不可恢复。"))
        {
            return;
        }

        await _session.Api.DeleteMaterialAsync(SelectedMaterial.Id);
        StatusMessage = "材料已删除";
        SelectedMaterial = null;
        await LoadMaterialsAsync(project.Id);
    });

    private async Task LoadMaterialsAsync(Guid projectId)
    {
        var result = await _session.Api.GetMaterialsAsync(
            projectId,
            SearchText,
            FilterEquipment?.Id);

        var currentId = SelectedMaterial?.Id;
        Materials.Clear();
        foreach (var material in result.Items)
        {
            Materials.Add(material);
        }
        if (currentId is not null)
        {
            SelectedMaterial = Materials.FirstOrDefault(x => x.Id == currentId);
        }
    }

    private async Task LoadEquipmentAsync(Guid projectId)
    {
        var equipment = await _session.Api.GetEquipmentsAsync(projectId, pageSize: 500);
        _equipment = equipment.Items.ToList();
        BuildEquipmentFilterOptions();
    }

    /// <summary>
    /// 保存后重新加载并选中该行。若当前搜索/设备筛选把保存项排除在外，
    /// 先重置筛选，避免用户看到"已保存"却找不到记录。
    /// </summary>
    private async Task ReloadAndSelectAsync(Guid projectId, Guid savedId)
    {
        await LoadMaterialsAsync(projectId);
        var saved = Materials.FirstOrDefault(x => x.Id == savedId);
        if (saved is null)
        {
            SearchText = string.Empty;
            FilterEquipment = EquipmentFilterOptions.FirstOrDefault();
            await LoadMaterialsAsync(projectId);
            saved = Materials.FirstOrDefault(x => x.Id == savedId);
        }

        SelectedMaterial = saved;
    }

    private void BuildEquipmentFilterOptions()
    {
        var currentId = FilterEquipment?.Id;
        EquipmentFilterOptions.Clear();
        EquipmentFilterOptions.Add(new MaterialEquipmentOption(null, "全部设备"));
        foreach (var equipment in _equipment)
        {
            EquipmentFilterOptions.Add(new MaterialEquipmentOption(
                equipment.Id, equipment.TagNo + "  " + equipment.Name));
        }

        FilterEquipment = EquipmentFilterOptions.FirstOrDefault(x => x.Id == currentId)
                          ?? EquipmentFilterOptions.FirstOrDefault();
    }

    private IReadOnlyList<MaterialEquipmentOption> BuildEditorEquipmentOptions()
    {
        var options = new List<MaterialEquipmentOption>
        {
            new(null, "（未关联设备）")
        };
        options.AddRange(_equipment.Select(x =>
            new MaterialEquipmentOption(x.Id, x.TagNo + "  " + x.Name)));
        return options;
    }

    private void Clear()
    {
        Materials.Clear();
        EquipmentFilterOptions.Clear();
        _equipment = new List<EquipmentDto>();
        SelectedMaterial = null;
        FilterEquipment = null;
    }

    private void OnProjectChanged(object? sender, EventArgs e) => _ = RefreshAsync();

    private static MaterialItemUpsertRequest ToRequest(MaterialEditResult result) => new()
    {
        EquipmentId = result.EquipmentId,
        Code = result.Code,
        Name = result.Name,
        Spec = result.Spec,
        Material = result.Material,
        Unit = result.Unit,
        Quantity = result.Quantity,
        Remark = result.Remark
    };
}
