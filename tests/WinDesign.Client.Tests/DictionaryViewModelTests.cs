using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Dictionaries;

namespace WinDesign.Client.Tests;

public class DictionaryViewModelTests
{
    [Fact]
    public async Task Refresh_DefaultsToCreatingState_WithoutAutoSelection()
    {
        var (api, _) = CreateData();
        var viewModel = CreateViewModel(api);
        await viewModel.RefreshAsync();

        Assert.Null(viewModel.SelectedType);
        Assert.False(viewModel.HasSelectedType);
        Assert.True(viewModel.IsCreatingType);
        Assert.Equal("新建编码类型", viewModel.TypeEditorTitle);

        Assert.Empty(viewModel.Items);
        Assert.Null(viewModel.SelectedItem);
        Assert.True(viewModel.IsCreatingItem);
        Assert.Equal("新建编码项", viewModel.ItemEditorTitle);
    }

    [Fact]
    public async Task SelectingType_EntersEdit_ButItemEditorStaysInCreateState()
    {
        var (api, type) = CreateData();
        var viewModel = CreateViewModel(api);
        await viewModel.RefreshAsync();

        viewModel.SelectedType = type;

        Assert.True(viewModel.HasSelectedType);
        Assert.False(viewModel.IsCreatingType);
        Assert.Equal("编辑编码类型：" + type.Code, viewModel.TypeEditorTitle);
        Assert.Equal(type.Code, viewModel.EditTypeCode);
        Assert.Equal(type.Name, viewModel.EditTypeName);

        Assert.Single(viewModel.Items);
        Assert.Null(viewModel.SelectedItem);
        Assert.True(viewModel.IsCreatingItem);
    }

    [Fact]
    public async Task SelectingItem_EntersEditState_AndKeepsItsValues()
    {
        var (api, type) = CreateData();
        var viewModel = CreateViewModel(api);
        await viewModel.RefreshAsync();
        viewModel.SelectedType = type;

        viewModel.SelectedItem = viewModel.Items[0];

        Assert.True(viewModel.HasSelectedItem);
        Assert.False(viewModel.IsCreatingItem);
        Assert.Equal("编辑编码项：ITC", viewModel.ItemEditorTitle);
        Assert.Equal("ITC", viewModel.EditItemCode);
        Assert.Equal("IT机柜", viewModel.EditItemName);
    }

    [Fact]
    public async Task CancelTypeEdit_RestoresSavedValues()
    {
        var (api, type) = CreateData();
        var viewModel = CreateViewModel(api);
        await viewModel.RefreshAsync();
        viewModel.SelectedType = type;
        viewModel.EditTypeName = "改过的名称";

        viewModel.CancelTypeEditCommand.Execute(null);

        Assert.Equal(type.Name, viewModel.EditTypeName);
        Assert.False(viewModel.IsCreatingType);
    }

    [Fact]
    public async Task CancelItemEdit_RestoresSavedValues()
    {
        var (api, type) = CreateData();
        var viewModel = CreateViewModel(api);
        await viewModel.RefreshAsync();
        viewModel.SelectedType = type;
        viewModel.SelectedItem = viewModel.Items[0];
        viewModel.EditItemName = "改过的名称";
        viewModel.EditItemActive = false;

        viewModel.CancelItemEditCommand.Execute(null);

        Assert.Equal("IT机柜", viewModel.EditItemName);
        Assert.True(viewModel.EditItemActive);
        Assert.False(viewModel.IsCreatingItem);
    }

    [Fact]
    public async Task NewItem_ReturnsToCreatingStateWithClearedFields()
    {
        var (api, type) = CreateData();
        var viewModel = CreateViewModel(api);
        await viewModel.RefreshAsync();
        viewModel.SelectedType = type;
        viewModel.SelectedItem = viewModel.Items[0];

        viewModel.NewItemCommand.Execute(null);

        Assert.Null(viewModel.SelectedItem);
        Assert.False(viewModel.HasSelectedItem);
        Assert.True(viewModel.IsCreatingItem);
        Assert.Equal("新建编码项", viewModel.ItemEditorTitle);
        Assert.Equal(string.Empty, viewModel.EditItemCode);
        Assert.Equal(string.Empty, viewModel.EditItemName);
        Assert.True(viewModel.EditItemActive);
    }

    [Fact]
    public async Task NewType_ReturnsToCreatingState_AndClearsItemPanel()
    {
        var (api, type) = CreateData();
        var viewModel = CreateViewModel(api);
        await viewModel.RefreshAsync();
        viewModel.SelectedType = type;
        viewModel.SelectedItem = viewModel.Items[0];

        viewModel.NewTypeCommand.Execute(null);

        Assert.Null(viewModel.SelectedType);
        Assert.True(viewModel.IsCreatingType);
        Assert.Empty(viewModel.Items);
        Assert.Null(viewModel.SelectedItem);
        Assert.True(viewModel.IsCreatingItem);
    }

    private static (FakeApiClient Api, DictionaryTypeDto Type) CreateData()
    {
        var api = new FakeApiClient();
        var type = new DictionaryTypeDto(
            Guid.NewGuid(),
            "EQUIPMENT_TYPE",
            "设备类型",
            "设备分类",
            true,
            0,
            1);
        api.DictionaryTypes.Add(type);
        api.EquipmentTypes.Add(new DictionaryItemDto(
            Guid.NewGuid(),
            type.Id,
            type.Code,
            "ITC",
            "IT机柜",
            0,
            true,
            null));
        return (api, type);
    }

    private static DictionaryViewModel CreateViewModel(FakeApiClient api) =>
        new(new SessionService(api));
}
