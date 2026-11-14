using WinDesign.AdminClient.ViewModels;
using WinDesign.Contracts.Numbering;
using WinDesign.Contracts.Pbs;

namespace WinDesign.Client.Tests;

public class NumberingRuleViewModelTests
{
    [Fact]
    public void Preview_UsesFieldNamesAndConfigurableLengths()
    {
        Assert.Equal(
            "{装置编号}-{设备类型编码}-XXXXA",
            EquipmentTagFormat.Preview(
                NumberingFields.UnitCode, "-", NumberingFields.EquipmentTypeCode, "-", 4, "A", 1));
        Assert.Equal(
            "{装置编号}{设备类型编码}XXXXXXABC",
            EquipmentTagFormat.Preview(
                NumberingFields.UnitCode, "", NumberingFields.EquipmentTypeCode, "", 6, "ABCD", 3));
        Assert.Equal("XXXX", EquipmentTagFormat.Preview(null, null, null, null, 4, null, 1));
        Assert.Equal("PT   ", EquipmentTagFormat.Fit("PT", 5));
        Assert.Equal("ABCDE", EquipmentTagFormat.Fit("ABCDEF", 5));
    }

    [Fact]
    public async Task Refresh_SelectsProjectAndLoadsRuleFields()
    {
        var api = new FakeApiClient();
        var root = api.AddDefaultPbsRoot();
        api.PbsNodes.Add(new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, root.Id, "1279", "乙烯装置", "Unit", 1, null, 0, 0));
        api.PbsNodes.Add(new PbsNodeDto(
            Guid.NewGuid(), api.Plant.Id, root.Id, "CDU", "常减压装置", "Unit", 2, null, 0, 0));
        api.NumberingRules[api.Project.Id] = new NumberingRuleDto(
            Guid.NewGuid(),
            api.Project.Id,
            NumberingFields.UnitCode,
            "-",
            4,
            NumberingFields.EquipmentTypeCode,
            string.Empty,
            5,
            6,
            "A",
            1);

        var viewModel = new NumberingRuleViewModel(api.CreateManagementSession());
        await viewModel.RefreshAsync();

        Assert.Single(viewModel.Projects);
        Assert.Equal(api.Project.Id, viewModel.SelectedProject!.Id);
        Assert.Equal(NumberingFields.UnitCode, viewModel.PrefixField);
        Assert.Equal(NumberingFields.EquipmentTypeCode, viewModel.EquipmentTypeField);
        Assert.Equal(6, viewModel.SequenceLength);
        Assert.Equal("XXXXXX", viewModel.SequenceDisplay);
        Assert.Equal("{装置编号}-{设备类型编码}XXXXXXA", viewModel.Preview);

        // 装置编号作为前缀时长度来自项目数据（1279/CDU 最大 4 位），且不可编辑。
        Assert.Equal(4, viewModel.PrefixLength);
        Assert.False(viewModel.PrefixLengthEditable);
    }

    [Fact]
    public async Task ManualPrefix_AllowsEditingLength()
    {
        var api = new FakeApiClient();
        api.NumberingRules[api.Project.Id] = new NumberingRuleDto(
            Guid.NewGuid(),
            api.Project.Id,
            string.Empty,
            "-",
            3,
            string.Empty,
            "-",
            5,
            4,
            string.Empty,
            1);

        var viewModel = new NumberingRuleViewModel(api.CreateManagementSession());
        await viewModel.RefreshAsync();

        Assert.True(viewModel.PrefixLengthEditable);
        Assert.Equal(3, viewModel.PrefixLength);
    }

    [Fact]
    public async Task Save_PersistsRuleForSelectedProject()
    {
        var api = new FakeApiClient();
        var viewModel = new NumberingRuleViewModel(api.CreateManagementSession());
        await viewModel.RefreshAsync();

        viewModel.PrefixField = NumberingFields.UnitCode;
        viewModel.PrefixSeparator = "-";
        viewModel.EquipmentTypeField = NumberingFields.EquipmentTypeCode;
        viewModel.TypeSeparator = "-";
        viewModel.TypeLength = 6;
        viewModel.SequenceLength = 5;
        viewModel.Suffix = "B";
        viewModel.SuffixLength = 2;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.True(api.NumberingRules.TryGetValue(api.Project.Id, out var saved));
        Assert.Equal(NumberingFields.UnitCode, saved!.PrefixField);
        Assert.Equal("-", saved.PrefixSeparator);
        Assert.Equal(NumberingFields.EquipmentTypeCode, saved.EquipmentTypeField);
        Assert.Equal("-", saved.TypeSeparator);
        Assert.Equal(6, saved.TypeLength);
        Assert.Equal(5, saved.SequenceLength);
        Assert.Equal("B", saved.Suffix);
        Assert.Equal(2, saved.SuffixLength);
        Assert.NotNull(viewModel.StatusMessage);
    }
}
