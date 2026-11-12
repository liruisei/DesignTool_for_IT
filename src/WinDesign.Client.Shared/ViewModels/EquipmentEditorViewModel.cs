using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Equipment;

namespace WinDesign.Client.ViewModels;

public sealed record CodeNameOption(string Code, string Name);

public sealed record EquipmentEditorContext(
    EquipmentDto? Equipment,
    bool IsCopy,
    IReadOnlyList<PbsNodeOption> PbsOptions,
    IReadOnlyList<DictionaryItemDto> EquipmentTypes,
    Guid? DefaultPbsNodeId = null,
    string? DefaultLoopNo = null);

public partial class EquipmentEditorViewModel : ObservableObject
{
    public IReadOnlyList<CodeNameOption> DisciplineOptions => DisciplineCatalog.All;

    public ObservableCollection<PbsNodeOption> PbsOptions { get; } = new();
    public ObservableCollection<DictionaryItemDto> EquipmentTypes { get; } = new();

    [ObservableProperty]
    private string title = "新建设备";

    [ObservableProperty]
    private string editTagNo = string.Empty;

    [ObservableProperty]
    private string editLoopNo = string.Empty;

    [ObservableProperty]
    private string editName = string.Empty;

    [ObservableProperty]
    private string? editTypeCode;

    [ObservableProperty]
    private string editDiscipline = "Instrument";

    /// <summary>专业由设备类型配置决定时锁定，避免与类型定义不一致。</summary>
    [ObservableProperty]
    private bool isDisciplineEditable = true;

    /// <summary>专业为 IT 时没有回路概念，回路号不可填写。</summary>
    public bool IsLoopNoEditable =>
        !string.Equals(EditDiscipline, "IT", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    private Guid? editPbsNodeId;

    [ObservableProperty]
    private string editLocation = string.Empty;

    [ObservableProperty]
    private string editPipeEquipmentNo = string.Empty;

    [ObservableProperty]
    private int editQuantity = 1;

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
    private string editDescription = string.Empty;

    [ObservableProperty]
    private string editProcessCondition = string.Empty;

    [ObservableProperty]
    private string editSpecification = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    public EquipmentEditorViewModel(EquipmentEditorContext context)
    {
        foreach (var option in context.PbsOptions)
        {
            PbsOptions.Add(option);
        }
        foreach (var type in context.EquipmentTypes)
        {
            EquipmentTypes.Add(type);
        }

        if (context.Equipment is null)
        {
            EditPbsNodeId = context.DefaultPbsNodeId
                            ?? PbsOptions.FirstOrDefault(x => x.Id is null)?.Id;
            EditLoopNo = context.DefaultLoopNo ?? string.Empty;
            return;
        }

        var equipment = context.Equipment;
        Title = context.IsCopy ? "复制设备" : "编辑设备：" + equipment.TagNo;
        EditTagNo = context.IsCopy ? string.Empty : equipment.TagNo;
        EditLoopNo = equipment.LoopNo ?? string.Empty;
        EditName = equipment.Name;
        EditTypeCode = equipment.TypeCode;

        // 设备类型配置了专业时以类型为准（上方赋值已自动带出），否则沿用设备自身专业。
        if (GetDisciplineFromType(equipment.TypeCode) is null)
        {
            EditDiscipline = equipment.Discipline;
        }

        EditPbsNodeId = equipment.PbsNodeId;
        EditLocation = equipment.Location ?? string.Empty;
        EditPipeEquipmentNo = equipment.PipeEquipmentNo ?? string.Empty;
        EditQuantity = equipment.Quantity;
        EditPurpose = equipment.Purpose ?? string.Empty;
        EditPidDrawingNo = equipment.PidDrawingNo ?? string.Empty;
        EditSpecNo = equipment.SpecNo ?? string.Empty;
        EditSpecPageNo = equipment.SpecPageNo ?? string.Empty;
        EditLoopDrawingNo = equipment.LoopDrawingNo ?? string.Empty;
        EditInstallDrawingNo = equipment.InstallDrawingNo ?? string.Empty;
        EditElectricalDrawingNo = equipment.ElectricalDrawingNo ?? string.Empty;
        EditHeatSupplyDrawingNo = equipment.HeatSupplyDrawingNo ?? string.Empty;
        EditAirSupplyDrawingNo = equipment.AirSupplyDrawingNo ?? string.Empty;
        EditLayingDrawingNo = equipment.LayingDrawingNo ?? string.Empty;
        EditJunctionBoxDrawingNo = equipment.JunctionBoxDrawingNo ?? string.Empty;
        EditKeyword = equipment.Keyword ?? string.Empty;
        EditIoSetting = equipment.IoSetting ?? string.Empty;
        EditIndexNo = equipment.IndexNo ?? string.Empty;
        EditSortOrder = equipment.SortOrder;
        EditDescription = equipment.Description ?? string.Empty;
        EditProcessCondition = equipment.ProcessCondition ?? string.Empty;
        EditSpecification = equipment.Specification ?? string.Empty;
    }

    /// <summary>
    /// 选择设备类型后自动带出专业并锁定；类型未配置专业时保留手工选择。
    /// </summary>
    partial void OnEditTypeCodeChanged(string? value)
    {
        var discipline = GetDisciplineFromType(value);
        if (discipline is null)
        {
            IsDisciplineEditable = true;
            return;
        }

        EditDiscipline = discipline;
        IsDisciplineEditable = false;
    }

    private string? GetDisciplineFromType(string? typeCode)
    {
        var type = EquipmentTypes.FirstOrDefault(x => x.Code == typeCode);
        return type is null
            ? null
            : type.Discipline ?? DictionaryItemExtras.GetDiscipline(type.ExtraJson);
    }

    partial void OnEditDisciplineChanged(string value)
    {
        OnPropertyChanged(nameof(IsLoopNoEditable));

        // 切到 IT 时清空残留回路号，避免把仪表回路带进 IT 设备。
        if (!IsLoopNoEditable)
        {
            EditLoopNo = string.Empty;
        }
    }

    public bool TryBuild(out EquipmentUpsertRequest? result, out string? error)
    {
        result = null;
        error = null;

        if (string.IsNullOrWhiteSpace(EditTagNo) || string.IsNullOrWhiteSpace(EditName))
        {
            error = "设备位号和名称不能为空";
            return false;
        }

        result = new EquipmentUpsertRequest
        {
            PbsNodeId = EditPbsNodeId,
            TagNo = EditTagNo.Trim(),
            LoopNo = IsLoopNoEditable ? NullIfEmpty(EditLoopNo) : null,
            Name = EditName.Trim(),
            TypeCode = NullIfEmpty(EditTypeCode),
            Discipline = EditDiscipline,
            Location = NullIfEmpty(EditLocation),
            PipeEquipmentNo = NullIfEmpty(EditPipeEquipmentNo),
            Quantity = EditQuantity > 0 ? EditQuantity : 1,
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
        };
        return true;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
