using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Numbering;

namespace WinDesign.Client.ViewModels;

public sealed record CodeNameOption(string Code, string Name);

public sealed record EquipmentEditorContext(
    EquipmentDto? Equipment,
    bool IsCopy,
    IReadOnlyList<PbsNodeOption> PbsOptions,
    IReadOnlyList<DictionaryItemDto> EquipmentTypes,
    Guid? DefaultPbsNodeId = null,
    string? DefaultLoopNo = null,
    NumberingRuleDto? NumberingRule = null,
    IReadOnlyDictionary<Guid, string>? UnitCodeByPbsNodeId = null,
    IReadOnlyList<string>? UnitCodeOptions = null,
    string? DefaultUnitCode = null);

public partial class EquipmentEditorViewModel : ObservableObject
{
    private readonly NumberingRuleDto? _rule;
    private readonly IReadOnlyDictionary<Guid, string> _unitCodeByPbsNodeId;
    private readonly bool _useComposedTag;

    public IReadOnlyList<CodeNameOption> DisciplineOptions => DisciplineCatalog.All;

    public ObservableCollection<PbsNodeOption> PbsOptions { get; } = new();
    public ObservableCollection<DictionaryItemDto> EquipmentTypes { get; } = new();
    /// <summary>编号规定前缀为装置编号时，可选的装置编号清单。</summary>
    public ObservableCollection<string> TagPrefixOptions { get; } = new();

    [ObservableProperty]
    private string title = "新建设备";

    [ObservableProperty]
    private string editTagNo = string.Empty;

    /// <summary>新建/复制且项目存在编号规定时，位号按规则分段拼装。</summary>
    public bool UseComposedTag => _useComposedTag;
    public bool UsePlainTagInput => !_useComposedTag;
    public bool IsPrefixAuto => _rule?.PrefixField == NumberingFields.UnitCode;
    public bool IsPrefixManual => !IsPrefixAuto;
    public bool IsTypeAuto => _rule?.EquipmentTypeField == NumberingFields.EquipmentTypeCode;
    public bool IsTypeManual => !IsTypeAuto;
    public string TagSeparator1 => _rule?.PrefixSeparator ?? string.Empty;
    public string TagSeparator2 => _rule?.TypeSeparator ?? string.Empty;
    public string TagComposeHint =>
        "按项目编号规定拼装；字段不足定义长度时在右侧补空格。";

    [ObservableProperty]
    private string tagPrefix = string.Empty;

    [ObservableProperty]
    private string tagTypePart = string.Empty;

    [ObservableProperty]
    private string tagSequence = string.Empty;

    [ObservableProperty]
    private string tagSuffix = string.Empty;

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
        _rule = context.NumberingRule;
        _unitCodeByPbsNodeId = context.UnitCodeByPbsNodeId ?? new Dictionary<Guid, string>();
        _useComposedTag = _rule is not null && (context.Equipment is null || context.IsCopy);

        foreach (var code in context.UnitCodeOptions ?? Array.Empty<string>())
        {
            TagPrefixOptions.Add(code);
        }
        if (!string.IsNullOrWhiteSpace(context.DefaultUnitCode))
        {
            TagPrefix = context.DefaultUnitCode;
        }

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
            RebuildTag();
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
        RebuildTag();
    }

    /// <summary>
    /// 选择设备类型后自动带出专业并锁定；类型未配置专业时保留手工选择。
    /// </summary>
    partial void OnEditTypeCodeChanged(string? value)
    {
        if (IsTypeAuto)
        {
            RebuildTag();
        }

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

    partial void OnEditPbsNodeIdChanged(Guid? value)
    {
        if (!IsPrefixAuto)
        {
            return;
        }

        if (value is Guid id && _unitCodeByPbsNodeId.TryGetValue(id, out var code))
        {
            TagPrefix = code;
        }
    }

    partial void OnTagPrefixChanged(string value) => RebuildTag();

    partial void OnTagTypePartChanged(string value) => RebuildTag();

    partial void OnTagSequenceChanged(string value) => RebuildTag();

    partial void OnTagSuffixChanged(string value) => RebuildTag();

    /// <summary>按项目编号规定拼装位号，字段不足定义长度时右侧补空格。</summary>
    private void RebuildTag()
    {
        if (!_useComposedTag || _rule is null)
        {
            return;
        }

        var typePart = IsTypeAuto ? EditTypeCode ?? string.Empty : TagTypePart;
        if (string.IsNullOrEmpty(TagPrefix) && string.IsNullOrEmpty(typePart) &&
            string.IsNullOrEmpty(TagSequence) && string.IsNullOrEmpty(TagSuffix))
        {
            EditTagNo = string.Empty;
            return;
        }

        EditTagNo =
            EquipmentTagFormat.Fit(TagPrefix, _rule.PrefixLength) +
            _rule.PrefixSeparator +
            EquipmentTagFormat.Fit(typePart, _rule.TypeLength) +
            _rule.TypeSeparator +
            EquipmentTagFormat.Fit(TagSequence, _rule.SequenceLength) +
            EquipmentTagFormat.Fit(TagSuffix, _rule.SuffixLength);
    }

    public bool TryBuild(out EquipmentUpsertRequest? result, out string? error)
    {
        result = null;
        error = null;

        if (_useComposedTag && string.IsNullOrWhiteSpace(TagSequence))
        {
            error = "请填写顺序号";
            return false;
        }
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
