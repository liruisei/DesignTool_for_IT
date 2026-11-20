using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Numbering;

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

    [Fact]
    public void SelectingEquipmentType_AutoFillsAndLocksDiscipline()
    {
        var viewModel = new EquipmentEditorViewModel(CreateContextWithTypeDiscipline("IT"));

        viewModel.EditTypeCode = "PT";

        Assert.Equal("IT", viewModel.EditDiscipline);
        Assert.False(viewModel.IsDisciplineEditable);
        Assert.False(viewModel.IsLoopNoEditable);
    }

    [Fact]
    public void SelectingTypeWithoutDiscipline_KeepsManualSelection()
    {
        var viewModel = new EquipmentEditorViewModel(CreateContext(null, false));

        viewModel.EditTypeCode = "PT";

        Assert.Equal("Instrument", viewModel.EditDiscipline);
        Assert.True(viewModel.IsDisciplineEditable);
    }

    [Fact]
    public void EditingEquipmentWithConfiguredType_UsesTypeDiscipline()
    {
        var viewModel = new EquipmentEditorViewModel(CreateContextWithTypeDiscipline("IT", CreateEquipment()));

        Assert.Equal("IT", viewModel.EditDiscipline);
        Assert.False(viewModel.IsDisciplineEditable);
    }

    [Fact]
    public void NewEquipment_WithNumberingRule_ComposesTagWithPadding()
    {
        var pbsNodeId = Guid.NewGuid();
        var rule = new NumberingRuleDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            NumberingFields.UnitCode,
            "-",
            4,
            NumberingFields.EquipmentTypeCode,
            "-",
            5,
            4,
            "A",
            1);
        var context = new EquipmentEditorContext(
            null,
            false,
            new[] { new PbsNodeOption(pbsNodeId, "PBS") },
            new[]
            {
                new DictionaryItemDto(Guid.NewGuid(), Guid.NewGuid(), "EQUIPMENT_TYPE",
                    "PT", "压力变送器", 1, true, null)
            },
            DefaultPbsNodeId: pbsNodeId,
            NumberingRule: rule,
            UnitCodeByPbsNodeId: new Dictionary<Guid, string> { [pbsNodeId] = "1279" },
            UnitCodeOptions: new[] { "1279", "CDU" },
            DefaultUnitCode: "1279");

        var viewModel = new EquipmentEditorViewModel(context);
        viewModel.EditName = "压力变送器";
        viewModel.EditTypeCode = "PT";
        viewModel.TagSequence = "12";
        viewModel.TagSuffix = "A";

        Assert.True(viewModel.UseComposedTag);
        Assert.True(viewModel.IsPrefixAuto);
        Assert.True(viewModel.IsTypeAuto);
        Assert.Equal(new[] { "1279", "CDU" }, viewModel.TagPrefixOptions);
        Assert.Equal("1279", viewModel.TagPrefix);
        Assert.Equal("1279-PT-12A", viewModel.EditTagNo);

        var ok = viewModel.TryBuild(out var result, out _);
        Assert.True(ok);
        Assert.Equal("1279-PT-12A", result!.TagNo);
    }

    [Fact]
    public void NewEquipment_WithManualTypeField_UsesSelectedTypeInTag()
    {
        var pbsNodeId = Guid.NewGuid();
        var rule = new NumberingRuleDto(
            Guid.NewGuid(), Guid.NewGuid(),
            NumberingFields.UnitCode, "-", 4,
            string.Empty, "-", 5, 4, "A", 1);
        var context = new EquipmentEditorContext(
            null,
            false,
            new[] { new PbsNodeOption(pbsNodeId, "PBS") },
            new[]
            {
                new DictionaryItemDto(Guid.NewGuid(), Guid.NewGuid(), "EQUIPMENT_TYPE",
                    "PT", "压力变送器", 1, true, null)
            },
            DefaultPbsNodeId: pbsNodeId,
            NumberingRule: rule,
            UnitCodeByPbsNodeId: new Dictionary<Guid, string> { [pbsNodeId] = "1279" },
            UnitCodeOptions: new[] { "1279" },
            DefaultUnitCode: "1279");

        var viewModel = new EquipmentEditorViewModel(context);
        Assert.True(viewModel.IsTypeManual);

        // 设备类型改为下拉后，即使编号规定未启用类型字段，选择类型也应写入位号类型段。
        viewModel.EditTypeCode = "PT";
        viewModel.TagSequence = "12";
        viewModel.TagSuffix = "A";

        Assert.Equal("PT", viewModel.TagTypePart);
        Assert.Equal("1279-PT-12A", viewModel.EditTagNo);
    }

    [Fact]
    public void ExistingEquipment_WithNumberingRule_UsesComposedTag()
    {
        var rule = new NumberingRuleDto(
            Guid.NewGuid(), Guid.NewGuid(),
            NumberingFields.UnitCode, "-", 4,
            NumberingFields.EquipmentTypeCode, "-", 5, 4, string.Empty, 1);
        var equipment = CreateEquipment();
        var context = new EquipmentEditorContext(
            equipment, false,
            new[] { new PbsNodeOption(null, "（未分配）") },
            Array.Empty<DictionaryItemDto>(),
            NumberingRule: rule);

        var viewModel = new EquipmentEditorViewModel(context);

        // 编辑已有设备也走编号规定，不再允许直接输入整段位号。
        Assert.True(viewModel.UseComposedTag);
        Assert.False(viewModel.UsePlainTagInput);
        // 历史位号 "PT-1001" 不符合当前规定，先保留原值，待用户调整分段后再重拼。
        Assert.Equal("PT-1001", viewModel.EditTagNo);
    }

    [Fact]
    public void ExistingEquipment_WithConformingTag_ParsesIntoFields()
    {
        var rule = new NumberingRuleDto(
            Guid.NewGuid(), Guid.NewGuid(),
            NumberingFields.UnitCode, "-", 4,
            NumberingFields.EquipmentTypeCode, "-", 5, 4, string.Empty, 1);
        var equipment = CreateEquipment() with { TagNo = "1279-PT   -0001A" };
        var context = new EquipmentEditorContext(
            equipment, false,
            new[] { new PbsNodeOption(null, "（未分配）") },
            Array.Empty<DictionaryItemDto>(),
            NumberingRule: rule);

        var viewModel = new EquipmentEditorViewModel(context);

        Assert.True(viewModel.UseComposedTag);
        Assert.Equal("1279", viewModel.TagPrefix);
        Assert.Equal("PT", viewModel.EditTypeCode);
        Assert.Equal("0001", viewModel.TagSequence);
        Assert.Equal("A", viewModel.TagSuffix);
        Assert.Equal("1279-PT-0001A", viewModel.EditTagNo);
    }

    [Fact]
    public void ExistingEquipment_RenamedTypeCode_KeepsTagSnapshot()
    {
        var rule = new NumberingRuleDto(
            Guid.NewGuid(), Guid.NewGuid(),
            NumberingFields.UnitCode, "-", 4,
            NumberingFields.EquipmentTypeCode, "-", 5, 4, string.Empty, 1);
        var equipment = CreateEquipment() with { TagNo = "1279-PT-0001", TypeCode = "PTA" };
        var context = new EquipmentEditorContext(
            equipment,
            false,
            new[] { new PbsNodeOption(null, "（未分配）") },
            new[]
            {
                new DictionaryItemDto(Guid.NewGuid(), Guid.NewGuid(),
                    "EQUIPMENT_TYPE", "PTA", "压力变送器", 1, true, null)
            },
            NumberingRule: rule);

        var viewModel = new EquipmentEditorViewModel(context);

        // 类型编码已在管理端由 PT 改成 PTA，但已实例化设备的位号仍沿用旧类型段。
        Assert.Equal("1279-PT-0001", viewModel.EditTagNo);

        // 只改顺序号：仍保留位号里的旧类型段。
        viewModel.TagSequence = "0002";
        Assert.Equal("1279-PT-0002", viewModel.EditTagNo);

        // 用户主动改选类型时才使用新编码重拼。
        viewModel.EditTypeCode = "PTB";
        Assert.Equal("1279-PTB-0002", viewModel.EditTagNo);
    }

    private static EquipmentEditorContext CreateContextWithTypeDiscipline(
        string discipline, EquipmentDto? equipment = null) => new(
        equipment,
        false,
        new[] { new PbsNodeOption(null, "（未分配）") },
        new[]
        {
            new DictionaryItemDto(Guid.NewGuid(), Guid.NewGuid(), "EQUIPMENT_TYPE", "PT", "压力变送器", 1, true, null)
            {
                Discipline = discipline
            }
        });

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
