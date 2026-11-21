using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Telecom;

namespace WinDesign.AdminClient.ViewModels;

/// <summary>
/// 设备样本库列表行。样本 DTO 不可变，这里包一层可修改的启用状态，
/// 列表中的勾选先留在本地，点击「保存」后统一提交（与用户管理一致）。
/// </summary>
public partial class DeviceRowItem : ObservableObject
{
    public DeviceRowItem(SwitchModelDto model)
    {
        Model = model;
        isActive = model.IsActive;
    }

    public SwitchModelDto Model { get; }
    public Guid Id => Model.Id;
    public string Code => Model.Code;
    public string Name => Model.Name;
    public string DisciplineText => DisciplineCatalog.Display(Model.Discipline);
    public string TypeText => string.IsNullOrWhiteSpace(Model.TypeName)
        ? Model.TypeCode ?? "—"
        : $"{Model.TypeCode}  {Model.TypeName}";
    public int ElectricalPorts => Model.ElectricalPorts;
    public int? UplinkPorts => Model.UplinkPorts;
    public int SortOrder => Model.SortOrder;

    [ObservableProperty]
    private bool isActive;

    /// <summary>与服务端保存值不同即表示有未保存的启用状态修改。</summary>
    public bool IsActiveDirty => IsActive != Model.IsActive;

    partial void OnIsActiveChanged(bool value) => OnPropertyChanged(nameof(IsActiveDirty));
}

/// <summary>
/// 设备样本库：维护设备的编码、名称、专业、设备类型与端口规格。
/// 专业与类型取自「编码定义 → 设备类型」，设计端电信统计按所选样本计算台数。
/// </summary>
public partial class SwitchLibraryViewModel : ViewModelBase
{
    private readonly SessionService _session;

    public ObservableCollection<DeviceRowItem> ModelRows { get; } = new();

    /// <summary>类型下拉按所选专业过滤，显示「编码 名称」。</summary>
    public ObservableCollection<CodeNameOption> TypeOptions { get; } = new();

    public IReadOnlyList<CodeNameOption> DisciplineOptions => DisciplineCatalog.All;

    private IReadOnlyList<DictionaryItemDto> _equipmentTypes = Array.Empty<DictionaryItemDto>();

    [ObservableProperty]
    private DeviceRowItem? selectedRow;

    [ObservableProperty]
    private string editorCode = string.Empty;

    [ObservableProperty]
    private string editorName = string.Empty;

    [ObservableProperty]
    private string editorDiscipline = "Telecom";

    [ObservableProperty]
    private string? editorTypeCode;

    [ObservableProperty]
    private int editorElectricalPorts = 24;

    [ObservableProperty]
    private string editorUplinkPorts = "4";

    [ObservableProperty]
    private bool editorIsActive = true;

    [ObservableProperty]
    private int editorSortOrder;

    [ObservableProperty]
    private string? editorRemark;

    [ObservableProperty]
    private bool isNew = true;

    [ObservableProperty]
    private string? statusMessage;

    /// <summary>列表中是否有未保存的启用/停用修改。</summary>
    public bool HasPendingActiveChanges => ModelRows.Any(x => x.IsActiveDirty);

    public string EditorTitle => IsNew ? "新建设备样本" : "编辑设备样本：" + EditorCode;

    public SwitchLibraryViewModel(SessionService session)
    {
        _session = session;
        Title = "设备样本库";
        _ = RefreshAsync();
    }

    public override Task RefreshAsync() => RunAsync(async () =>
    {
        var models = await _session.Api.GetSwitchModelsAsync();
        _equipmentTypes = await _session.Api.GetDictionaryItemsAsync("EQUIPMENT_TYPE");
        SetRows(models, SelectedRow?.Id);
        RefreshTypeOptions();
    });

    partial void OnSelectedRowChanged(DeviceRowItem? value)
    {
        if (value is not null)
        {
            LoadEditor(value.Model);
        }
    }

    partial void OnEditorCodeChanged(string value) => OnPropertyChanged(nameof(EditorTitle));

    partial void OnIsNewChanged(bool value) => OnPropertyChanged(nameof(EditorTitle));

    [RelayCommand]
    private void NewModel()
    {
        SelectedRow = null;
        StartNew();
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(EditorCode) || string.IsNullOrWhiteSpace(EditorName))
        {
            throw new ApiException("设备编码和名称不能为空");
        }
        if (string.IsNullOrWhiteSpace(EditorDiscipline) || string.IsNullOrWhiteSpace(EditorTypeCode))
        {
            throw new ApiException("请选择专业和设备类型");
        }

        if (EditorElectricalPorts < 0)
        {
            throw new ApiException("端口数量不能为负数");
        }

        var selectedType = _equipmentTypes.FirstOrDefault(x =>
            string.Equals(x.Code, EditorTypeCode, StringComparison.OrdinalIgnoreCase));
        if (selectedType?.Name.Contains("交换机", StringComparison.Ordinal) == true &&
            EditorElectricalPorts <= 0)
        {
            throw new ApiException("交换机类型的端口数量必须大于 0");
        }

        int? uplinkPorts = null;
        if (!string.IsNullOrWhiteSpace(EditorUplinkPorts))
        {
            if (!int.TryParse(EditorUplinkPorts, out var parsed) || parsed < 0)
            {
                throw new ApiException("上联口数量必须是非负整数");
            }
            uplinkPorts = parsed;
        }

        var request = new SwitchModelUpsertRequest(
            EditorCode.Trim(),
            EditorName.Trim(),
            EditorDiscipline,
            EditorTypeCode,
            EditorElectricalPorts,
            uplinkPorts,
            EditorIsActive,
            EditorSortOrder,
            string.IsNullOrWhiteSpace(EditorRemark) ? null : EditorRemark.Trim());

        var saved = IsNew || SelectedRow is null
            ? await _session.Api.CreateSwitchModelAsync(request)
            : await _session.Api.UpdateSwitchModelAsync(SelectedRow.Id, request);

        await ReloadAndSelectAsync(saved.Id);
        StatusMessage = "设备样本已保存";
    });

    /// <summary>批量保存列表中勾选的启用/停用状态（「新建」右侧的「保存」）。</summary>
    [RelayCommand]
    private Task SaveActiveChanges() => RunAsync(async () =>
    {
        var dirty = ModelRows.Where(x => x.IsActiveDirty).ToList();
        if (dirty.Count == 0)
        {
            StatusMessage = "没有需要保存的启用状态";
            return;
        }

        try
        {
            foreach (var row in dirty)
            {
                await _session.Api.ToggleSwitchModelActiveAsync(row.Id);
            }

            StatusMessage = $"已保存 {dirty.Count} 个设备的启用状态";
        }
        finally
        {
            // 无论成功与否都刷新，让列表与服务器状态一致。
            var models = await _session.Api.GetSwitchModelsAsync();
            SetRows(models, SelectedRow?.Id);
        }
    });

    [RelayCommand]
    private Task DeleteAsync() => RunAsync(async () =>
    {
        if (SelectedRow is null)
        {
            throw new ApiException("请先选择要删除的设备样本");
        }

        await _session.Api.DeleteSwitchModelAsync(SelectedRow.Id);
        var models = await _session.Api.GetSwitchModelsAsync();
        SetRows(models, null);
        StatusMessage = "设备样本已删除";
    });

    private async Task ReloadAndSelectAsync(Guid selectedId)
    {
        var models = await _session.Api.GetSwitchModelsAsync();
        SetRows(models, selectedId);
    }

    private void SetRows(IReadOnlyList<SwitchModelDto> models, Guid? selectedId)
    {
        foreach (var row in ModelRows)
        {
            row.PropertyChanged -= OnRowPropertyChanged;
        }
        ModelRows.Clear();

        foreach (var model in models)
        {
            var row = new DeviceRowItem(model);
            row.PropertyChanged += OnRowPropertyChanged;
            ModelRows.Add(row);
        }
        OnPropertyChanged(nameof(HasPendingActiveChanges));

        SelectedRow = selectedId is Guid id
            ? ModelRows.FirstOrDefault(x => x.Id == id)
            : ModelRows.FirstOrDefault();
        if (SelectedRow is null)
        {
            StartNew();
        }
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DeviceRowItem.IsActive) or nameof(DeviceRowItem.IsActiveDirty))
        {
            OnPropertyChanged(nameof(HasPendingActiveChanges));
        }
    }

    /// <summary>按当前专业重建类型下拉；已选类型不在新专业下时清空，避免保存出跨专业的组合。</summary>
    private void RefreshTypeOptions()
    {
        TypeOptions.Clear();
        foreach (var item in _equipmentTypes
                     .Where(x => string.Equals(x.Discipline, EditorDiscipline, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(x => x.SortOrder)
                     .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase))
        {
            TypeOptions.Add(new CodeNameOption(item.Code, $"{item.Code}  {item.Name}"));
        }

        if (EditorTypeCode is { Length: > 0 } code &&
            !TypeOptions.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            EditorTypeCode = null;
        }
    }

    partial void OnEditorDisciplineChanged(string value) => RefreshTypeOptions();

    private void StartNew()
    {
        IsNew = true;
        EditorCode = string.Empty;
        EditorName = string.Empty;
        // 新建时沿用当前样本的专业，避免落到一个没有可选类型的专业。
        EditorDiscipline = SelectedRow?.Model.Discipline
                           ?? ModelRows.FirstOrDefault()?.Model.Discipline
                           ?? "Telecom";
        EditorTypeCode = null;
        EditorElectricalPorts = 24;
        EditorUplinkPorts = "4";
        EditorIsActive = true;
        EditorSortOrder = (ModelRows.Count == 0 ? 0 : ModelRows.Max(x => x.SortOrder)) + 1;
        EditorRemark = string.Empty;
        StatusMessage = null;
        ErrorMessage = null;
    }

    private void LoadEditor(SwitchModelDto model)
    {
        IsNew = false;
        EditorCode = model.Code;
        EditorName = model.Name;
        EditorDiscipline = model.Discipline ?? "Telecom";
        RefreshTypeOptions();
        EditorTypeCode = model.TypeCode;
        EditorElectricalPorts = model.ElectricalPorts;
        EditorUplinkPorts = model.UplinkPorts?.ToString() ?? string.Empty;
        EditorIsActive = model.IsActive;
        EditorSortOrder = model.SortOrder;
        EditorRemark = model.Remark;
        StatusMessage = null;
        ErrorMessage = null;
    }
}
