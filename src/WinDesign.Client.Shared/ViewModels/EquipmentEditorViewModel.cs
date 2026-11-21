using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
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
    string? DefaultUnitCode = null,
    string PbsFieldLabel = "所属建筑物");

public partial class EquipmentEditorViewModel : ObservableObject
{
    private readonly NumberingRuleDto? _rule;
    private readonly IReadOnlyDictionary<Guid, string> _unitCodeByPbsNodeId;
    private readonly bool _useComposedTag;
    /// <summary>新建或复制设备时必须明确所属建筑物。</summary>
    private readonly bool _requiresBuilding;
    /// <summary>载入已有位号期间禁止重拼，避免字段尚未填好就把原值覆盖掉。</summary>
    private bool _suppressRebuild;
    /// <summary>
    /// 编辑已有设备时，位号里实际使用的类型段。设备类型编码在管理端被改名后，
    /// 已实例化设备的位号仍是当初的快照，重拼时要继续沿用该类型段。
    /// </summary>
    private string? _tagTypePart;

    public IReadOnlyList<CodeNameOption> DisciplineOptions => DisciplineCatalog.All;

    /// <summary>PBS 归属字段的显示名：设计端为「所属建筑物」，管理端仍为「所属 PBS」。</summary>
    public string PbsFieldLabel { get; }

    public ObservableCollection<PbsNodeOption> PbsOptions { get; } = new();

    /// <summary>按位号前缀（装置编码）过滤后的建筑物下拉视图；前缀不是装置编码时不过滤。</summary>
    public ICollectionView? PbsOptionsView { get; }
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
        "按项目编号规定拼装；字段不足定义长度时原样保留，超出时按定义长度截断。";

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
        PbsFieldLabel = context.PbsFieldLabel;
        // 有编号规定时，新建、复制和编辑都按同一格式分段拼装位号。
        _useComposedTag = _rule is not null;
        // 新建与复制都要求明确所属建筑物；编辑旧设备保持原有归属即可。
        _requiresBuilding = context.Equipment is null || context.IsCopy;

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

        PbsOptionsView = CollectionViewSource.GetDefaultView(PbsOptions);
        PbsOptionsView.Filter = FilterPbsOptions;
        PbsOptionsView.Refresh();

        // 历史数据可能引用了已不存在的类型编码：补一个占位项，避免打开编辑时下拉框清空类型。
        if (context.Equipment?.TypeCode is { Length: > 0 } legacyTypeCode &&
            !EquipmentTypes.Any(x => string.Equals(x.Code, legacyTypeCode, StringComparison.OrdinalIgnoreCase)))
        {
            EquipmentTypes.Add(new DictionaryItemDto(
                Guid.Empty, Guid.Empty, "EQUIPMENT_TYPE", legacyTypeCode,
                "（已不存在的设备类型）", int.MaxValue, true, null));
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

        // 载入期间先禁止重拼，等各字段和现有位号解析完成后再统一生成。
        _suppressRebuild = true;
        try
        {
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
        finally
        {
            _suppressRebuild = false;
        }

        if (!_useComposedTag)
        {
            return;
        }

        // 复制：按默认装置与所选类型重新编号；编辑：把现有位号反解析回各字段。
        bool parsed;
        _suppressRebuild = true;
        try
        {
            parsed = context.IsCopy || TryFillTagParts(equipment.TagNo);
        }
        finally
        {
            _suppressRebuild = false;
        }

        if (parsed)
        {
            RebuildTag();
        }
        else
        {
            // 历史位号不符合当前编号规定：先保留原值，用户调整分段后再按规则重拼。
            EditTagNo = equipment.TagNo;
        }
    }

    /// <summary>
    /// 选择设备类型后自动带出专业并锁定；类型未配置专业时保留手工选择。
    /// </summary>
    partial void OnEditTypeCodeChanged(string? value)
    {
        if (IsTypeAuto)
        {
            // 载入阶段不清除，保留位号中反解析出的类型段；用户主动改选类型时才跟随新编码。
            if (!_suppressRebuild)
            {
                _tagTypePart = null;
            }
            RebuildTag();
        }
        else
        {
            // 编号规定未把设备类型纳入位号时，位号里的类型段仍跟随所选设备类型。
            TagTypePart = value ?? string.Empty;
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

    partial void OnTagPrefixChanged(string value)
    {
        // 前缀是装置编码时，所属建筑物下拉只显示该装置下的建筑物。
        PbsOptionsView?.Refresh();
        if (!_suppressRebuild &&
            EditPbsNodeId is Guid selectedId &&
            PbsOptionsView is not null &&
            PbsOptionsView.Cast<PbsNodeOption>().All(x => x.Id != selectedId))
        {
            EditPbsNodeId = null;
        }

        RebuildTag();
    }

    /// <summary>
    /// 位号前缀对应某个装置编码时，只保留该装置下的建筑物；
    /// 手工前缀或没有装置映射时不过滤，历史值占位项始终保留。
    /// </summary>
    private bool FilterPbsOptions(object item)
    {
        if (item is not PbsNodeOption option || option.Id is not Guid id)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(TagPrefix) ||
            _unitCodeByPbsNodeId.Count == 0 ||
            !_unitCodeByPbsNodeId.Values.Contains(TagPrefix, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return !_unitCodeByPbsNodeId.TryGetValue(id, out var unitCode) ||
               string.Equals(unitCode, TagPrefix, StringComparison.OrdinalIgnoreCase);
    }

    partial void OnTagTypePartChanged(string value) => RebuildTag();

    partial void OnTagSequenceChanged(string value) => RebuildTag();

    partial void OnTagSuffixChanged(string value) => RebuildTag();

    /// <summary>按项目编号规定拼装位号；字段不足定义长度时原样保留，超出时截断。</summary>
    private void RebuildTag()
    {
        if (_suppressRebuild || !_useComposedTag || _rule is null)
        {
            return;
        }

        // 编辑已有设备时优先沿用位号里的类型段，避免类型编码改名后旧位号被重拼改写。
        var typePart = IsTypeAuto
            ? _tagTypePart ?? EditTypeCode ?? string.Empty
            : TagTypePart;
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

    /// <summary>
    /// 把已有位号按编号规定拆回前缀、类型、顺序号、后缀，供编辑时继续按规则拼装。
    /// 历史位号不符合当前规定时返回 false，由调用方保留原值。
    /// </summary>
    private bool TryFillTagParts(string tagNo)
    {
        if (_rule is null || string.IsNullOrWhiteSpace(tagNo))
        {
            return false;
        }

        var rest = tagNo.Trim();
        _tagTypePart = null;

        // 前缀
        if (!string.IsNullOrEmpty(_rule.PrefixSeparator))
        {
            var index = rest.IndexOf(_rule.PrefixSeparator, StringComparison.Ordinal);
            if (index <= 0)
            {
                return false;
            }

            TagPrefix = rest[..index].Trim();
            rest = rest[(index + _rule.PrefixSeparator.Length)..];
        }
        else
        {
            var length = _rule.PrefixLength;
            if (length <= 0 || rest.Length < length)
            {
                return false;
            }

            TagPrefix = rest[..length].Trim();
            rest = rest[length..];
        }

        // 类型段：自动模式记录位号里的实际类型段（类型编码可能已在管理端改名）；
        // 手动模式把段内容写入位号类型部分。
        if (!string.IsNullOrEmpty(_rule.TypeSeparator))
        {
            // 类型编码本身可能包含分隔符（如 SW-ACC）：优先按已知类型编码整体匹配，
            // 避免被第一个分隔符截断后，把 ACC-0 之类的片段误当成顺序号。
            var expectedTypeCode = (IsTypeAuto ? EditTypeCode : TagTypePart)?.Trim();
            string typeSegment;
            if (!string.IsNullOrEmpty(expectedTypeCode) &&
                rest.StartsWith(expectedTypeCode + _rule.TypeSeparator, StringComparison.Ordinal))
            {
                typeSegment = expectedTypeCode;
                rest = rest[(expectedTypeCode.Length + _rule.TypeSeparator.Length)..];
            }
            else
            {
                // 类型编码已在管理端改名等情况下，退回最后一个分隔符切分。
                var index = rest.LastIndexOf(_rule.TypeSeparator, StringComparison.Ordinal);
                if (index < 0)
                {
                    return false;
                }

                typeSegment = rest[..index].Trim();
                rest = rest[(index + _rule.TypeSeparator.Length)..];
            }

            if (IsTypeAuto)
            {
                _tagTypePart = typeSegment.Length == 0 ? null : typeSegment;
            }
            else
            {
                TagTypePart = typeSegment;
            }
        }
        else
        {
            var length = _rule.TypeLength;
            if (length <= 0 || rest.Length < length)
            {
                return false;
            }

            var typeSegment = rest[..length].Trim();
            if (IsTypeAuto)
            {
                _tagTypePart = typeSegment.Length == 0 ? null : typeSegment;
            }
            else
            {
                TagTypePart = typeSegment;
            }

            rest = rest[length..];
        }

        // 顺序号 + 后缀
        var sequenceLength = _rule.SequenceLength;
        if (sequenceLength <= 0)
        {
            return false;
        }

        if (rest.Length < sequenceLength)
        {
            // 拼装时 Fit 不会补零，实际顺序号可能短于定义长度；
            // 剩余整段作为顺序号，避免整条位号被判定为不符合规则而清空字段。
            TagSequence = rest.Trim();
            TagSuffix = string.Empty;
            return TagSequence.Length > 0;
        }

        TagSequence = rest[..sequenceLength].Trim();
        TagSuffix = rest[sequenceLength..].Trim();
        return true;
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
        if (_requiresBuilding && EditPbsNodeId is null)
        {
            error = "请选择所属建筑物";
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
