using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Numbering;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Projects;

namespace WinDesign.AdminClient.ViewModels;

/// <summary>
/// 项目级设备位号编号规定：先选择项目，再定义位号使用哪些字段及每段长度。
/// 下拉选项表示字段名（如「装置编号」「设备类型编码」），不是具体数据值；
/// 留空表示该段允许后续手动输入。前缀为装置编号时长度由数据决定，不可编辑。
/// </summary>
public partial class NumberingRuleViewModel : ViewModelBase
{
    private readonly SessionService _session;
    private bool _suppressRuleLoad;
    private int _unitCodeMaxLength = EquipmentTagFormat.DefaultPrefixLength;

    public ObservableCollection<ProjectDto> Projects { get; } = new();

    public IReadOnlyList<CodeNameOption> PrefixFieldOptions { get; } = new[]
    {
        new CodeNameOption(string.Empty, "（空 / 手动输入）"),
        new CodeNameOption(NumberingFields.UnitCode, "装置编号")
    };

    public IReadOnlyList<CodeNameOption> EquipmentTypeFieldOptions { get; } = new[]
    {
        new CodeNameOption(string.Empty, "（空 / 手动输入）"),
        new CodeNameOption(NumberingFields.EquipmentTypeCode, "设备类型编码")
    };

    public IReadOnlyList<string> SeparatorOptions { get; } = new[] { string.Empty, "-" };

    [ObservableProperty]
    private ProjectDto? selectedProject;

    [ObservableProperty]
    private string prefixField = string.Empty;

    [ObservableProperty]
    private string prefixSeparator = string.Empty;

    [ObservableProperty]
    private int prefixLength = EquipmentTagFormat.DefaultPrefixLength;

    [ObservableProperty]
    private string equipmentTypeField = string.Empty;

    [ObservableProperty]
    private string typeSeparator = string.Empty;

    [ObservableProperty]
    private int typeLength = EquipmentTagFormat.DefaultTypeLength;

    [ObservableProperty]
    private int sequenceLength = EquipmentTagFormat.DefaultSequenceLength;

    [ObservableProperty]
    private string suffix = string.Empty;

    [ObservableProperty]
    private int suffixLength = EquipmentTagFormat.DefaultSuffixLength;

    [ObservableProperty]
    private string? statusMessage;

    /// <summary>装置编号作为前缀时，长度由项目中的装置编号数据决定，不允许修改。</summary>
    public bool PrefixLengthEditable =>
        !string.Equals(PrefixField, NumberingFields.UnitCode, StringComparison.Ordinal);

    public string SequenceDisplay => EquipmentTagFormat.SequencePlaceholder(SequenceLength);

    public string Preview => EquipmentTagFormat.Preview(
        PrefixField, PrefixSeparator, EquipmentTypeField, TypeSeparator,
        SequenceLength, Suffix, SuffixLength);

    public string TypeCodeHint =>
        "每段长度可自定义；前缀选择「装置编号」时长度由项目数据决定（灰色不可改）。" +
        "选择「空」时该段留给使用者手动输入。";

    public NumberingRuleViewModel(SessionService session)
    {
        _session = session;
        Title = "编号规定";
        _ = RefreshAsync();
    }

    public override Task RefreshAsync() => RunAsync(async () =>
    {
        var result = await _session.Api.GetProjectsAsync();
        var selectedId = SelectedProject?.Id;
        Projects.Clear();
        foreach (var project in result.Items)
        {
            Projects.Add(project);
        }

        _suppressRuleLoad = true;
        SelectedProject = Projects.FirstOrDefault(x => x.Id == selectedId)
                          ?? Projects.FirstOrDefault();
        _suppressRuleLoad = false;

        if (SelectedProject is not null)
        {
            await LoadRuleAsync(SelectedProject);
        }
        else
        {
            ClearRule();
            ErrorMessage = "请先在管理端创建项目";
        }
    });

    partial void OnSelectedProjectChanged(ProjectDto? value)
    {
        if (_suppressRuleLoad || value is null)
        {
            return;
        }

        _ = RunAsync(() => LoadRuleAsync(value));
    }

    partial void OnPrefixFieldChanged(string value)
    {
        if (string.Equals(value, NumberingFields.UnitCode, StringComparison.Ordinal))
        {
            PrefixLength = _unitCodeMaxLength;
        }

        OnPropertyChanged(nameof(PrefixLengthEditable));
        OnPropertyChanged(nameof(Preview));
    }

    partial void OnPrefixSeparatorChanged(string value) => OnPropertyChanged(nameof(Preview));

    partial void OnPrefixLengthChanged(int value) => OnPropertyChanged(nameof(Preview));

    partial void OnEquipmentTypeFieldChanged(string value) => OnPropertyChanged(nameof(Preview));

    partial void OnTypeSeparatorChanged(string value) => OnPropertyChanged(nameof(Preview));

    partial void OnTypeLengthChanged(int value) => OnPropertyChanged(nameof(Preview));

    partial void OnSequenceLengthChanged(int value)
    {
        OnPropertyChanged(nameof(SequenceDisplay));
        OnPropertyChanged(nameof(Preview));
    }

    partial void OnSuffixChanged(string value) => OnPropertyChanged(nameof(Preview));

    partial void OnSuffixLengthChanged(int value) => OnPropertyChanged(nameof(Preview));

    [RelayCommand]
    private Task Save() => RunAsync(async () =>
    {
        var project = SelectedProject
            ?? throw new ApiException("请先选择项目");

        if (PrefixLength < 1 || TypeLength < 1)
        {
            throw new ApiException("前缀/设备类型字段长度必须大于 0");
        }
        if (SequenceLength is < 1 or > 12)
        {
            throw new ApiException("顺序号长度必须在 1 到 12 之间");
        }
        if (SuffixLength is < 0 or > 16)
        {
            throw new ApiException("后缀长度必须在 0 到 16 之间");
        }

        var saved = await _session.Api.SaveNumberingRuleAsync(project.Id, new UpsertNumberingRuleRequest(
            PrefixField,
            PrefixSeparator,
            PrefixLength,
            EquipmentTypeField,
            TypeSeparator,
            TypeLength,
            SequenceLength,
            Suffix.Trim(),
            SuffixLength));

        ApplyRule(saved);
        StatusMessage = $"已保存项目「{project.Code}」的编号规定";
    });

    private async Task LoadRuleAsync(ProjectDto project)
    {
        _unitCodeMaxLength = await LoadUnitCodeMaxLengthAsync(project);
        var rule = await _session.Api.GetNumberingRuleAsync(project.Id);
        ApplyRule(rule);
        StatusMessage = null;
        ErrorMessage = null;
    }

    private void ApplyRule(NumberingRuleDto rule)
    {
        PrefixField = rule.PrefixField;
        PrefixSeparator = rule.PrefixSeparator;
        PrefixLength = string.Equals(rule.PrefixField, NumberingFields.UnitCode, StringComparison.Ordinal)
            ? _unitCodeMaxLength
            : rule.PrefixLength;
        EquipmentTypeField = rule.EquipmentTypeField;
        TypeSeparator = rule.TypeSeparator;
        TypeLength = rule.TypeLength;
        SequenceLength = rule.SequenceLength;
        Suffix = rule.Suffix;
        SuffixLength = rule.SuffixLength;
    }

    /// <summary>装置编号字段长度取项目 PBS 范围内装置编号的最大长度。</summary>
    private async Task<int> LoadUnitCodeMaxLengthAsync(ProjectDto project)
    {
        var nodes = await _session.Api.GetPbsNodesAsync(project.PlantId);
        var byId = nodes.ToDictionary(x => x.Id);

        bool InScope(PbsNodeDto node)
        {
            if (project.PbsRootNodeId is not Guid rootId)
            {
                return true;
            }

            var current = node;
            var visited = new HashSet<Guid>();
            while (visited.Add(current.Id))
            {
                if (current.Id == rootId)
                {
                    return true;
                }
                if (current.ParentId is not Guid parentId || !byId.TryGetValue(parentId, out var parent))
                {
                    return false;
                }
                current = parent;
            }
            return false;
        }

        var lengths = nodes
            .Where(x => string.Equals(x.NodeType, "Unit", StringComparison.OrdinalIgnoreCase))
            .Where(InScope)
            .Select(x => x.Code?.Length ?? 0)
            .Where(x => x > 0)
            .ToList();

        return lengths.Count == 0
            ? EquipmentTagFormat.DefaultPrefixLength
            : lengths.Max();
    }

    private void ClearRule()
    {
        PrefixField = string.Empty;
        PrefixSeparator = string.Empty;
        PrefixLength = EquipmentTagFormat.DefaultPrefixLength;
        EquipmentTypeField = string.Empty;
        TypeSeparator = string.Empty;
        TypeLength = EquipmentTagFormat.DefaultTypeLength;
        SequenceLength = EquipmentTagFormat.DefaultSequenceLength;
        Suffix = string.Empty;
        SuffixLength = EquipmentTagFormat.DefaultSuffixLength;
        StatusMessage = null;
    }
}
