using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using WinDesign.Contracts.Materials;

namespace WinDesign.Client.ViewModels;

/// <summary>设备下拉项；Id 为 null 表示不关联设备。</summary>
public sealed record MaterialEquipmentOption(Guid? Id, string Display);

public sealed record MaterialEditorContext(
    MaterialItemDto? Material,
    IReadOnlyList<MaterialEquipmentOption> EquipmentOptions,
    bool IsReadOnly);

public sealed record MaterialEditResult(
    Guid? EquipmentId,
    string Code,
    string Name,
    string? Spec,
    string? Material,
    string Unit,
    decimal Quantity,
    string? Remark);

public partial class MaterialEditorViewModel : ObservableObject
{
    private readonly MaterialItemDto? _material;

    public ObservableCollection<MaterialEquipmentOption> EquipmentOptions { get; } = new();

    [ObservableProperty]
    private string title = "新建材料/附件";

    [ObservableProperty]
    private Guid? selectedEquipmentId;

    [ObservableProperty]
    private string editCode = string.Empty;

    [ObservableProperty]
    private string editName = string.Empty;

    [ObservableProperty]
    private string editSpec = string.Empty;

    [ObservableProperty]
    private string editMaterial = string.Empty;

    [ObservableProperty]
    private string editUnit = "个";

    [ObservableProperty]
    private string editQuantity = "0";

    [ObservableProperty]
    private string editRemark = string.Empty;

    [ObservableProperty]
    private string? errorMessage;

    public bool IsReadOnly { get; }

    public bool IsEditable => !IsReadOnly;

    public MaterialEditorViewModel(MaterialEditorContext context)
    {
        _material = context.Material;
        IsReadOnly = context.IsReadOnly;

        foreach (var option in context.EquipmentOptions)
        {
            EquipmentOptions.Add(option);
        }

        if (_material is null)
        {
            SelectedEquipmentId = null;
            return;
        }

        Title = IsReadOnly ? $"查看材料：{_material.Code}" : $"编辑材料：{_material.Code}";
        SelectedEquipmentId = _material.EquipmentId;
        EditCode = _material.Code;
        EditName = _material.Name;
        EditSpec = _material.Spec ?? string.Empty;
        EditMaterial = _material.Material ?? string.Empty;
        EditUnit = _material.Unit;
        EditQuantity = _material.Quantity.ToString("0.####", CultureInfo.InvariantCulture);
        EditRemark = _material.Remark ?? string.Empty;
    }

    public bool TryBuild(out MaterialEditResult? result, out string? error)
    {
        result = null;
        error = null;

        if (IsReadOnly)
        {
            error = "当前账号为只读，不能保存材料";
            return false;
        }

        var code = EditCode.Trim();
        var name = EditName.Trim();
        if (code.Length == 0 || name.Length == 0)
        {
            error = "材料编码和名称不能为空";
            return false;
        }

        if (!decimal.TryParse(EditQuantity, NumberStyles.Number, CultureInfo.CurrentCulture, out var quantity))
        {
            error = "数量必须是数字";
            return false;
        }
        if (quantity < 0)
        {
            error = "数量不能为负数";
            return false;
        }

        result = new MaterialEditResult(
            SelectedEquipmentId,
            code,
            name,
            NullIfEmpty(EditSpec),
            NullIfEmpty(EditMaterial),
            string.IsNullOrWhiteSpace(EditUnit) ? "个" : EditUnit.Trim(),
            quantity,
            NullIfEmpty(EditRemark));
        return true;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
