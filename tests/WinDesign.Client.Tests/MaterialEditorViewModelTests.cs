using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Materials;

namespace WinDesign.Client.Tests;

public class MaterialEditorViewModelTests
{
    [Fact]
    public void NewMaterial_RequiresCodeAndName()
    {
        var viewModel = CreateEditor(new MaterialEditorContext(
            null,
            new[] { new MaterialEquipmentOption(null, "（未关联设备）") },
            IsReadOnly: false));

        var ok = viewModel.TryBuild(out _, out var error);

        Assert.False(ok);
        Assert.Contains("编码和名称", error);
    }

    [Fact]
    public void NewMaterial_DefaultsUnitAndQuantity()
    {
        var viewModel = CreateEditor(new MaterialEditorContext(
            null,
            new[] { new MaterialEquipmentOption(null, "（未关联设备）") },
            IsReadOnly: false));
        viewModel.EditCode = "MAT-100";
        viewModel.EditName = "电缆";

        var ok = viewModel.TryBuild(out var result, out var error);

        Assert.True(ok, error);
        Assert.Equal("个", result!.Unit);
        Assert.Equal(0m, result.Quantity);
        Assert.Null(result.EquipmentId);
    }

    [Fact]
    public void Quantity_MustBeNumericAndNonNegative()
    {
        var viewModel = CreateEditor(new MaterialEditorContext(
            null,
            new[] { new MaterialEquipmentOption(null, "（未关联设备）") },
            IsReadOnly: false));
        viewModel.EditCode = "MAT-100";
        viewModel.EditName = "电缆";

        viewModel.EditQuantity = "abc";
        Assert.False(viewModel.TryBuild(out _, out var numberError));
        Assert.Contains("数字", numberError);

        viewModel.EditQuantity = "-1";
        Assert.False(viewModel.TryBuild(out _, out var negativeError));
        Assert.Contains("负数", negativeError);

        viewModel.EditQuantity = "12.5";
        Assert.True(viewModel.TryBuild(out var result, out _));
        Assert.Equal(12.5m, result!.Quantity);
    }

    [Fact]
    public void ExistingMaterial_LoadsFieldsAndEquipment()
    {
        var equipmentId = Guid.NewGuid();
        var material = new MaterialItemDto
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            EquipmentId = equipmentId,
            Code = "MAT-001",
            Name = "不锈钢管",
            Spec = "Φ14×2",
            Material = "316SS",
            Unit = "米",
            Quantity = 120m,
            Remark = "seed"
        };
        var viewModel = CreateEditor(new MaterialEditorContext(
            material,
            new[] { new MaterialEquipmentOption(equipmentId, "PT-1001  压力变送器") },
            IsReadOnly: false));

        Assert.Equal("MAT-001", viewModel.EditCode);
        Assert.Equal("120", viewModel.EditQuantity);
        Assert.Equal(equipmentId, viewModel.SelectedEquipmentId);

        viewModel.EditQuantity = "150";
        Assert.True(viewModel.TryBuild(out var result, out _));
        Assert.Equal(150m, result!.Quantity);
        Assert.Equal(equipmentId, result.EquipmentId);
    }

    [Fact]
    public void ReadOnlyMaterial_CannotBuild()
    {
        var viewModel = CreateEditor(new MaterialEditorContext(
            new MaterialItemDto { Id = Guid.NewGuid(), Code = "MAT-001", Name = "材料", Unit = "个" },
            new[] { new MaterialEquipmentOption(null, "（未关联设备）") },
            IsReadOnly: true));

        var ok = viewModel.TryBuild(out _, out var error);

        Assert.False(ok);
        Assert.Contains("只读", error);
    }

    private static MaterialEditorViewModel CreateEditor(MaterialEditorContext context) => new(context);
}
