using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Contracts.Equipment;

namespace WinDesign.Client.ViewModels;

/// <summary>弹窗中的设备下拉项；只暴露 Id 与展示文本，避免把整份设备 DTO 带进编辑器。</summary>
public sealed record EquipmentSpecEquipmentOption(Guid Id, string Display);

public sealed record EquipmentSpecEditorContext(
    EquipmentSpecDto? Spec,
    IReadOnlyList<EquipmentSpecEquipmentOption> EquipmentOptions,
    bool IsReadOnly,
    Guid? DefaultEquipmentId = null);

public sealed record EquipmentSpecEditResult(
    Guid EquipmentId,
    string SpecCode,
    string SpecName,
    IReadOnlyList<SpecParameterDto> Parameters);

/// <summary>参数表的一行。Key/Value 直接双向绑定 DataGrid 单元格。</summary>
public partial class SpecParameterRow : ObservableObject
{
    [ObservableProperty]
    private string key = string.Empty;

    [ObservableProperty]
    private string value = string.Empty;
}

public partial class EquipmentSpecEditorViewModel : ObservableObject
{
    private readonly EquipmentSpecDto? _spec;

    public ObservableCollection<EquipmentSpecEquipmentOption> EquipmentOptions { get; } = new();
    public ObservableCollection<SpecParameterRow> Parameters { get; } = new();

    [ObservableProperty]
    private string title = "新建规格书";

    [ObservableProperty]
    private Guid? selectedEquipmentId;

    [ObservableProperty]
    private string editSpecCode = string.Empty;

    [ObservableProperty]
    private string editSpecName = string.Empty;

    [ObservableProperty]
    private SpecParameterRow? selectedParameter;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string? contentWarning;

    public bool IsReadOnly { get; }

    public bool IsEditable => !IsReadOnly;

    /// <summary>新建时可改归属设备；编辑时设备不可变更。</summary>
    public bool IsEquipmentEditable => _spec is null && IsEditable;

    public EquipmentSpecEditorViewModel(EquipmentSpecEditorContext context)
    {
        _spec = context.Spec;
        IsReadOnly = context.IsReadOnly;

        foreach (var option in context.EquipmentOptions)
        {
            EquipmentOptions.Add(option);
        }

        if (_spec is null)
        {
            SelectedEquipmentId = context.DefaultEquipmentId ?? EquipmentOptions.FirstOrDefault()?.Id;
            AddBlankRow();
            return;
        }

        Title = IsReadOnly ? $"查看规格书：{_spec.SpecCode}" : $"编辑规格书：{_spec.SpecCode}";
        SelectedEquipmentId = _spec.EquipmentId;
        EditSpecCode = _spec.SpecCode;
        EditSpecName = _spec.SpecName;
        ContentWarning = _spec.ContentWarning;

        foreach (var parameter in _spec.Parameters ?? Array.Empty<SpecParameterDto>())
        {
            Parameters.Add(new SpecParameterRow { Key = parameter.Key, Value = parameter.Value });
        }

        // 参数全空时给编辑者一个空行，避免面对一张无法输入的空表。
        if (IsEditable && Parameters.Count == 0)
        {
            AddBlankRow();
        }
    }

    [RelayCommand]
    private void AddParameter()
    {
        var row = AddBlankRow();
        SelectedParameter = row;
    }

    [RelayCommand]
    private void RemoveParameter()
    {
        if (SelectedParameter is not null)
        {
            Parameters.Remove(SelectedParameter);
        }
    }

    /// <summary>把当前表单转换为 API 请求；参数规范化（空行、重复名、长度）统一交给 codec。</summary>
    public bool TryBuild(out EquipmentSpecEditResult? result, out string? error)
    {
        result = null;
        error = null;

        if (IsReadOnly)
        {
            error = "当前账号为只读，不能保存规格书";
            return false;
        }

        var equipmentId = _spec?.EquipmentId ?? SelectedEquipmentId;
        if (equipmentId is not Guid selectedEquipmentId)
        {
            error = "请选择设备";
            return false;
        }

        var code = EditSpecCode.Trim();
        var name = EditSpecName.Trim();
        if (code.Length == 0 || name.Length == 0)
        {
            error = "规格书编号和名称不能为空";
            return false;
        }

        var rows = Parameters.Select(x => new SpecParameterDto
        {
            Key = x.Key,
            Value = x.Value
        });
        if (!SpecContentCodec.TryNormalize(rows, out var normalized, out error))
        {
            return false;
        }

        result = new EquipmentSpecEditResult(selectedEquipmentId, code, name, normalized);
        return true;
    }

    private SpecParameterRow AddBlankRow()
    {
        var row = new SpecParameterRow();
        Parameters.Add(row);
        return row;
    }
}
