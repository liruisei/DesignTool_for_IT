using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
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
    public string SwitchTypeText => TypeName(Model.SwitchType);
    public int ElectricalPorts => Model.ElectricalPorts;
    public int? UplinkPorts => Model.UplinkPorts;
    public int SortOrder => Model.SortOrder;

    [ObservableProperty]
    private bool isActive;

    /// <summary>与服务端保存值不同即表示有未保存的启用状态修改。</summary>
    public bool IsActiveDirty => IsActive != Model.IsActive;

    partial void OnIsActiveChanged(bool value) => OnPropertyChanged(nameof(IsActiveDirty));

    public static string TypeName(string? code) => code?.ToLowerInvariant() switch
    {
        "access" => "接入交换机",
        "poe" => "PoE 交换机",
        "cabinet" => "机柜",
        _ => "其他设备"
    };
}

/// <summary>
/// 设备样本库：维护接入交换机、PoE 交换机、机柜等设备的编码、名称与端口规格。
/// 设计端电信统计按 Access / PoE 两类样本计算交换机台数。
/// </summary>
public partial class SwitchLibraryViewModel : ViewModelBase
{
    private readonly SessionService _session;

    public ObservableCollection<DeviceRowItem> ModelRows { get; } = new();

    public IReadOnlyList<CodeNameOption> DeviceTypeOptions { get; } = new[]
    {
        new CodeNameOption("Access", "接入交换机"),
        new CodeNameOption("PoE", "PoE 交换机"),
        new CodeNameOption("Cabinet", "机柜"),
        new CodeNameOption("Other", "其他设备")
    };

    [ObservableProperty]
    private DeviceRowItem? selectedRow;

    [ObservableProperty]
    private string editorCode = string.Empty;

    [ObservableProperty]
    private string editorName = string.Empty;

    [ObservableProperty]
    private string editorSwitchType = "Access";

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
        SetRows(models, SelectedRow?.Id);
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

        var isSwitchType = EditorSwitchType is "Access" or "PoE";
        if (EditorElectricalPorts < 0)
        {
            throw new ApiException("端口数量不能为负数");
        }
        if (isSwitchType && EditorElectricalPorts <= 0)
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
            EditorSwitchType,
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

    private void StartNew()
    {
        IsNew = true;
        EditorCode = string.Empty;
        EditorName = string.Empty;
        EditorSwitchType = "Access";
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
        EditorSwitchType = model.SwitchType;
        EditorElectricalPorts = model.ElectricalPorts;
        EditorUplinkPorts = model.UplinkPorts?.ToString() ?? string.Empty;
        EditorIsActive = model.IsActive;
        EditorSortOrder = model.SortOrder;
        EditorRemark = model.Remark;
        StatusMessage = null;
        ErrorMessage = null;
    }
}
