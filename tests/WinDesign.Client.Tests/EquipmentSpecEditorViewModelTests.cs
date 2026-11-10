using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Equipment;

namespace WinDesign.Client.Tests;

public class EquipmentSpecEditorViewModelTests
{
    private static readonly Guid EquipmentId = Guid.NewGuid();

    [Fact]
    public void NewSpec_RequiresCodeAndName()
    {
        var viewModel = CreateEditor(CreateContext(null));

        var ok = viewModel.TryBuild(out _, out var error);

        Assert.False(ok);
        Assert.Contains("编号和名称", error);
    }

    [Fact]
    public void NewSpec_DropsBlankRowsAndBuildsParameters()
    {
        var viewModel = CreateEditor(CreateContext(null));
        viewModel.EditSpecCode = "SPEC-NEW";
        viewModel.EditSpecName = "新规格书";
        viewModel.Parameters.Add(new SpecParameterRow { Key = "range", Value = "0~1.6MPa" });
        viewModel.Parameters.Add(new SpecParameterRow { Key = " ", Value = " " });

        var ok = viewModel.TryBuild(out var result, out var error);

        Assert.True(ok, error);
        Assert.NotNull(result);
        Assert.Equal(EquipmentId, result!.EquipmentId);
        var parameter = Assert.Single(result.Parameters);
        Assert.Equal("range", parameter.Key);
    }

    [Fact]
    public void NewSpec_DuplicateParameterNames_FailValidation()
    {
        var viewModel = CreateEditor(CreateContext(null));
        viewModel.EditSpecCode = "SPEC-NEW";
        viewModel.EditSpecName = "新规格书";
        viewModel.Parameters.Add(new SpecParameterRow { Key = "range", Value = "a" });
        viewModel.Parameters.Add(new SpecParameterRow { Key = "RANGE", Value = "b" });

        var ok = viewModel.TryBuild(out _, out var error);

        Assert.False(ok);
        Assert.Contains("重复", error);
    }

    [Fact]
    public void ExistingSpec_LoadsParametersAndKeepsEquipmentFixed()
    {
        var spec = new EquipmentSpecDto
        {
            Id = Guid.NewGuid(),
            EquipmentId = EquipmentId,
            EquipmentTagNo = "PT-1001",
            EquipmentName = "压力变送器",
            SpecCode = "SPEC-PT-1001",
            SpecName = "压力变送器规格书",
            Parameters = new[]
            {
                new SpecParameterDto { Key = "range", Value = "0~1.6MPa" },
                new SpecParameterDto { Key = "output", Value = "4~20mA" }
            },
            Version = 1
        };
        var viewModel = CreateEditor(CreateContext(spec));

        Assert.Equal(2, viewModel.Parameters.Count);
        Assert.Equal("range", viewModel.Parameters[0].Key);
        Assert.False(viewModel.IsEquipmentEditable);

        viewModel.EditSpecName = "更新规格书";
        var ok = viewModel.TryBuild(out var result, out var error);

        Assert.True(ok, error);
        Assert.Equal(EquipmentId, result!.EquipmentId);
        Assert.Equal("更新规格书", result.SpecName);
    }

    [Fact]
    public void ReadOnlySpec_CannotBuild()
    {
        var spec = new EquipmentSpecDto
        {
            Id = Guid.NewGuid(),
            EquipmentId = EquipmentId,
            SpecCode = "SPEC-PT-1001",
            SpecName = "压力变送器规格书"
        };
        var viewModel = CreateEditor(CreateContext(spec, isReadOnly: true));

        var ok = viewModel.TryBuild(out _, out var error);

        Assert.False(ok);
        Assert.Contains("只读", error);
        Assert.True(viewModel.IsReadOnly);
    }

    private static EquipmentSpecEditorContext CreateContext(
        EquipmentSpecDto? spec,
        bool isReadOnly = false) => new(
        spec,
        new[] { new EquipmentSpecEquipmentOption(EquipmentId, "PT-1001  压力变送器") },
        isReadOnly);

    private static EquipmentSpecEditorViewModel CreateEditor(EquipmentSpecEditorContext context) =>
        new(context);
}
