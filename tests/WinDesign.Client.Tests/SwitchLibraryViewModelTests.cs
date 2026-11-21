using WinDesign.AdminClient.ViewModels;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Telecom;

namespace WinDesign.Client.Tests;

public class SwitchLibraryViewModelTests
{
    [Fact]
    public async Task SaveActiveChanges_CommitsGridCheckboxAndReloads()
    {
        var api = new FakeApiClient();
        AddSeedModels(api);
        var viewModel = new SwitchLibraryViewModel(api.CreateManagementSession());
        await viewModel.RefreshAsync();

        Assert.Equal(2, viewModel.ModelRows.Count);
        Assert.False(viewModel.HasPendingActiveChanges);

        viewModel.ModelRows[0].IsActive = false;
        Assert.True(viewModel.HasPendingActiveChanges);

        await viewModel.SaveActiveChangesCommand.ExecuteAsync(null);

        Assert.False(viewModel.HasPendingActiveChanges);
        Assert.False(api.SwitchModels.Single(x => x.Code == "SW-24A").IsActive);
        Assert.True(viewModel.ModelRows.Single(x => x.Code == "SW-24A") is { IsActive: false });
    }

    [Fact]
    public async Task Save_CabinetWithZeroPorts_IsAllowed()
    {
        var api = new FakeApiClient();
        AddSeedModels(api);
        var viewModel = new SwitchLibraryViewModel(api.CreateManagementSession());
        await viewModel.RefreshAsync();

        viewModel.EditorCode = "CAB-TEST";
        viewModel.EditorName = "测试机柜";
        viewModel.EditorDiscipline = "Telecom";
        viewModel.EditorTypeCode = "CAB";
        viewModel.EditorElectricalPorts = 0;
        viewModel.EditorUplinkPorts = string.Empty;

        await viewModel.SaveCommand.ExecuteAsync(null);

        var saved = api.SwitchModels.Single(x => x.Code == "CAB-TEST");
        Assert.Equal("CAB", saved.TypeCode);
        Assert.Equal(0, saved.ElectricalPorts);
        Assert.Contains(viewModel.ModelRows, x => x.Code == "CAB-TEST" && x.TypeText.Contains("机柜"));
    }

    [Fact]
    public async Task Save_SwitchWithZeroPorts_IsRejected()
    {
        var api = new FakeApiClient();
        AddSeedModels(api);
        var viewModel = new SwitchLibraryViewModel(api.CreateManagementSession());
        await viewModel.RefreshAsync();

        viewModel.EditorCode = "SW-BAD";
        viewModel.EditorName = "错误交换机";
        viewModel.EditorDiscipline = "Telecom";
        viewModel.EditorTypeCode = "SW";
        viewModel.EditorElectricalPorts = 0;

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(viewModel.ErrorMessage);
        Assert.DoesNotContain(api.SwitchModels, x => x.Code == "SW-BAD");
    }

    private static void AddSeedModels(FakeApiClient api)
    {
        api.EquipmentTypes.Add(new DictionaryItemDto(
            Guid.NewGuid(), Guid.NewGuid(), "EQUIPMENT_TYPE", "SW", "交换机", 1, true, null)
        {
            Discipline = "Telecom"
        });
        api.EquipmentTypes.Add(new DictionaryItemDto(
            Guid.NewGuid(), Guid.NewGuid(), "EQUIPMENT_TYPE", "CAB", "机柜", 2, true, null)
        {
            Discipline = "Telecom"
        });

        api.SwitchModels.Add(new SwitchModelDto(
            Guid.NewGuid(), "SW-24A", "24 口接入", "Telecom", "SW", "交换机", 24, 4, true, 1, null));
        api.SwitchModels.Add(new SwitchModelDto(
            Guid.NewGuid(), "CAB-42U", "42U 机柜", "Telecom", "CAB", "机柜", 0, null, true, 2, null));
    }
}
