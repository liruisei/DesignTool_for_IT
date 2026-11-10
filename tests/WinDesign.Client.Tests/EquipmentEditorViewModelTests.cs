using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Equipment;

namespace WinDesign.Client.Tests;

public class EquipmentEditorViewModelTests
{
    [Fact]
    public void NewEquipment_Defaults()
    {
        var context = CreateContext(null, false);

        var viewModel = new EquipmentEditorViewModel(context);

        Assert.Equal("新建设备", viewModel.Title);
        Assert.Equal("Instrument", viewModel.EditDiscipline);
        Assert.True(viewModel.IsLoopNoEditable);
        Assert.Equal(1, viewModel.EditQuantity);
        Assert.True(string.IsNullOrEmpty(viewModel.EditTagNo));
        Assert.Null(viewModel.EditPbsNodeId);
    }

    [Fact]
    public void CopyEquipment_ClearsTagAndKeepsFields()
    {
        var source = CreateEquipment();
        var viewModel = new EquipmentEditorViewModel(CreateContext(source, true));

        Assert.Equal("复制设备", viewModel.Title);
        Assert.True(string.IsNullOrEmpty(viewModel.EditTagNo));
        Assert.Equal("LIC-1001", viewModel.EditLoopNo);
        Assert.Equal("压力变送器", viewModel.EditName);
        Assert.Equal(2, viewModel.EditQuantity);
    }

    [Fact]
    public void EditEquipment_PopulatesFields()
    {
        var source = CreateEquipment();
        var viewModel = new EquipmentEditorViewModel(CreateContext(source, false));

        Assert.StartsWith("编辑设备", viewModel.Title);
        Assert.Equal("PT-1001", viewModel.EditTagNo);
        Assert.Equal("PID-001", viewModel.EditPidDrawingNo);
        Assert.Equal("AI-01", viewModel.EditIoSetting);
        Assert.Equal(3, viewModel.EditSortOrder);
    }

    [Fact]
    public void TryBuild_EmptyTag_ReturnsError()
    {
        var viewModel = new EquipmentEditorViewModel(CreateContext(null, false))
        {
            EditTagNo = string.Empty,
            EditName = "设备"
        };

        var ok = viewModel.TryBuild(out _, out var error);

        Assert.False(ok);
        Assert.Contains("不能为空", error);
    }

    [Fact]
    public void TryBuild_MapsAllFields()
    {
        var viewModel = new EquipmentEditorViewModel(CreateContext(null, false))
        {
            EditTagNo = "PT-3001",
            EditLoopNo = "LIC-3001",
            EditName = "压力变送器",
            EditDiscipline = "Instrument",
            EditQuantity = 3,
            EditPidDrawingNo = "PID-3001",
            EditSpecNo = "SPEC-3001",
            EditIoSetting = "AI-03"
        };

        var ok = viewModel.TryBuild(out var result, out _);

        Assert.True(ok);
        Assert.Equal("PT-3001", result!.TagNo);
        Assert.Equal("LIC-3001", result.LoopNo);
        Assert.Equal(3, result.Quantity);
        Assert.Equal("PID-3001", result.PidDrawingNo);
        Assert.Equal("SPEC-3001", result.SpecNo);
        Assert.Equal("AI-03", result.IoSetting);
    }

    [Fact]
    public void DisciplineChangedToIt_DisablesLoopNo_AndClearsValue()
    {
        var viewModel = new EquipmentEditorViewModel(CreateContext(null, false))
        {
            EditLoopNo = "LIC-3001"
        };

        viewModel.EditDiscipline = "IT";

        Assert.False(viewModel.IsLoopNoEditable);
        Assert.Equal(string.Empty, viewModel.EditLoopNo);
    }

    [Fact]
    public void TryBuild_ItDiscipline_OmitsLoopNo()
    {
        var viewModel = new EquipmentEditorViewModel(CreateContext(null, false))
        {
            EditTagNo = "SW-3001",
            EditName = "接入交换机",
            EditDiscipline = "IT",
            EditLoopNo = "LIC-3001"
        };

        var ok = viewModel.TryBuild(out var result, out _);

        Assert.True(ok);
        Assert.Null(result!.LoopNo);
    }

    private static EquipmentEditorContext CreateContext(EquipmentDto? equipment, bool isCopy) => new(
        equipment,
        isCopy,
        new[] { new PbsNodeOption(null, "（未分配）") },
        new[]
        {
            new DictionaryItemDto(Guid.NewGuid(), Guid.NewGuid(), "EQUIPMENT_TYPE", "PT", "压力变送器", 1, true, null)
        });

    private static EquipmentDto CreateEquipment() => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        TagNo = "PT-1001",
        LoopNo = "LIC-1001",
        Name = "压力变送器",
        TypeCode = "PT",
        Discipline = "Instrument",
        Quantity = 2,
        PidDrawingNo = "PID-001",
        IoSetting = "AI-01",
        SortOrder = 3,
        CreatedAt = DateTime.UtcNow
    };
}
