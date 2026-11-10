using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Equipment;

namespace WinDesign.DesignClient.ViewModels;

public sealed record EquipmentFilterOption(Guid? Id, string Display);

/// <summary>
/// 独立规格书页：列出当前项目范围内所有设备的规格书，支持按设备/关键字过滤。
/// Viewer 可查看，新建/编辑/删除由服务端角色校验，客户端按钮同步禁用。
/// </summary>
public partial class EquipmentSpecIndexViewModel : ViewModelBase
{
    private readonly SessionService _session;
    private readonly ProjectContext _context;
    private readonly IEquipmentSpecDialogService _dialogs;
    private List<EquipmentDto> _equipment = new();
    private bool _equipmentLoaded;

    public ObservableCollection<EquipmentSpecDto> Specs { get; } = new();
    public ObservableCollection<EquipmentFilterOption> EquipmentFilterOptions { get; } = new();

    [ObservableProperty]
    private EquipmentSpecDto? selectedSpec;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private EquipmentFilterOption? filterEquipment;

    [ObservableProperty]
    private string? statusMessage;

    public ProjectContext Context => _context;

    public bool CanWrite => _session.IsAdmin || _session.IsDesigner;

    public bool IsReadOnly => !CanWrite;

    /// <summary>设备列表加载完成且项目内至少有一台设备时才允许新建。</summary>
    public bool CanCreate => CanWrite && _equipmentLoaded && _equipment.Count > 0;

    public EquipmentSpecIndexViewModel(
        SessionService session,
        ProjectContext context,
        IEquipmentSpecDialogService? dialogs = null)
    {
        _session = session;
        _context = context;
        _dialogs = dialogs ?? new EquipmentSpecDialogService();
        Title = "规格书";
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

        // 设备与规格书各自独立加载：设备接口失败时，规格书列表仍应可用。
        await Task.WhenAll(LoadEquipmentAsync(project.Id), LoadSpecsAsync(project.Id));
    });

    [RelayCommand]
    private Task SearchAsync() => RunAsync(async () =>
    {
        var project = _context.CurrentProject ?? throw new ApiException("请先选择工作区项目");
        await LoadSpecsAsync(project.Id);
    });

    [RelayCommand]
    private Task NewSpecAsync() => RunAsync(async () =>
    {
        var project = _context.CurrentProject ?? throw new ApiException("请先选择工作区项目");
        if (!CanWrite)
        {
            ErrorMessage = "当前账号没有新建规格书的权限";
            return;
        }
        if (!_equipmentLoaded)
        {
            ErrorMessage = "设备列表尚未加载完成，请稍后重试";
            return;
        }
        if (_equipment.Count == 0)
        {
            ErrorMessage = "当前项目没有设备，无法创建规格书";
            return;
        }

        var context = new EquipmentSpecEditorContext(
            null,
            BuildEditorEquipmentOptions(),
            IsReadOnly: false,
            DefaultEquipmentId: FilterEquipment?.Id);
        var result = _dialogs.EditSpec(context);
        if (result is null)
        {
            return;
        }

        var saved = await _session.Api.CreateEquipmentSpecAsync(result.EquipmentId, ToRequest(result));
        StatusMessage = "规格书已创建";
        await ReloadAndSelectAsync(project.Id, saved.Id);
    });

    [RelayCommand]
    private async Task OpenSpecAsync()
    {
        if (SelectedSpec is null)
        {
            ErrorMessage = "请先选择要打开的规格书";
            return;
        }

        var project = _context.CurrentProject;
        if (project is null)
        {
            ErrorMessage = "请先选择工作区项目";
            return;
        }

        var selected = SelectedSpec;
        var context = new EquipmentSpecEditorContext(
            selected,
            BuildEditorEquipmentOptions(),
            IsReadOnly: IsReadOnly);
        var result = _dialogs.EditSpec(context);
        if (result is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var saved = await _session.Api.UpdateEquipmentSpecAsync(selected.Id, ToRequest(result));
            StatusMessage = "规格书已保存";
            await ReloadAndSelectAsync(project.Id, saved.Id);
        });
    }

    [RelayCommand]
    private Task DeleteSpecAsync() => RunAsync(async () =>
    {
        var project = _context.CurrentProject ?? throw new ApiException("请先选择工作区项目");
        if (!CanWrite)
        {
            ErrorMessage = "当前账号没有删除规格书的权限";
            return;
        }
        if (SelectedSpec is null)
        {
            ErrorMessage = "请选择要删除的规格书";
            return;
        }

        if (!_dialogs.Confirm($"确定删除规格书 {SelectedSpec.SpecCode} 吗？此操作不可恢复。"))
        {
            return;
        }

        await _session.Api.DeleteEquipmentSpecAsync(SelectedSpec.Id);
        StatusMessage = "规格书已删除";
        SelectedSpec = null;
        await LoadSpecsAsync(project.Id);
    });

    private async Task LoadSpecsAsync(Guid projectId)
    {
        var result = await _session.Api.GetEquipmentSpecsAsync(
            projectId,
            SearchText,
            FilterEquipment?.Id);

        var currentId = SelectedSpec?.Id;
        Specs.Clear();
        foreach (var spec in result.Items)
        {
            Specs.Add(spec);
        }
        if (currentId is not null)
        {
            SelectedSpec = Specs.FirstOrDefault(x => x.Id == currentId);
        }
    }

    private async Task LoadEquipmentAsync(Guid projectId)
    {
        var equipment = await _session.Api.GetEquipmentsAsync(projectId, pageSize: 500);
        _equipment = equipment.Items.ToList();
        _equipmentLoaded = true;
        BuildEquipmentFilterOptions();
        OnPropertyChanged(nameof(CanCreate));
    }

    /// <summary>
    /// 保存后重新加载并选中该行。若当前搜索/设备筛选把保存项排除在外，
    /// 先重置筛选，避免用户看到"已保存"却找不到记录。
    /// </summary>
    private async Task ReloadAndSelectAsync(Guid projectId, Guid savedId)
    {
        await LoadSpecsAsync(projectId);
        var saved = Specs.FirstOrDefault(x => x.Id == savedId);
        if (saved is null)
        {
            SearchText = string.Empty;
            FilterEquipment = EquipmentFilterOptions.FirstOrDefault();
            await LoadSpecsAsync(projectId);
            saved = Specs.FirstOrDefault(x => x.Id == savedId);
        }

        SelectedSpec = saved;
    }

    private void BuildEquipmentFilterOptions()
    {
        var currentId = FilterEquipment?.Id;
        EquipmentFilterOptions.Clear();
        EquipmentFilterOptions.Add(new EquipmentFilterOption(null, "全部设备"));
        foreach (var equipment in _equipment)
        {
            EquipmentFilterOptions.Add(new EquipmentFilterOption(
                equipment.Id, equipment.TagNo + "  " + equipment.Name));
        }

        FilterEquipment = EquipmentFilterOptions.FirstOrDefault(x => x.Id == currentId)
                          ?? EquipmentFilterOptions.FirstOrDefault();
    }

    private IReadOnlyList<EquipmentSpecEquipmentOption> BuildEditorEquipmentOptions() =>
        _equipment
            .Select(x => new EquipmentSpecEquipmentOption(x.Id, x.TagNo + "  " + x.Name))
            .ToList();

    private void Clear()
    {
        Specs.Clear();
        EquipmentFilterOptions.Clear();
        _equipment = new List<EquipmentDto>();
        _equipmentLoaded = false;
        SelectedSpec = null;
        FilterEquipment = null;
        OnPropertyChanged(nameof(CanCreate));
    }

    private void OnProjectChanged(object? sender, EventArgs e) => _ = RefreshAsync();

    private static EquipmentSpecUpsertRequest ToRequest(EquipmentSpecEditResult result) => new()
    {
        SpecCode = result.SpecCode,
        SpecName = result.SpecName,
        Parameters = result.Parameters
    };
}
