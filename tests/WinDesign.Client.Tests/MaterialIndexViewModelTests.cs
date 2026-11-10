using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Materials;
using WinDesign.DesignClient.ViewModels;

namespace WinDesign.Client.Tests;

public class MaterialIndexViewModelTests
{
    [Fact]
    public async Task Refresh_LoadsMaterialsAndEquipmentFilter()
    {
        var api = new FakeApiClient();
        var equipment = AddEquipment(api);
        AddMaterial(api, equipment);
        var viewModel = CreateViewModel(api);

        await viewModel.RefreshAsync();

        Assert.Single(viewModel.Materials);
        Assert.Equal(2, viewModel.EquipmentFilterOptions.Count);
        Assert.True(viewModel.CanWrite);
    }

    [Fact]
    public async Task NewMaterial_OpensDialogAndCreates()
    {
        var api = new FakeApiClient();
        AddEquipment(api);
        var dialogs = new FakeMaterialDialogService
        {
            NextResult = new MaterialEditResult(
                null, "MAT-NEW", "新电缆", "2×1.5", "PVC", "米", 50m, null)
        };
        var viewModel = CreateViewModel(api, dialogs);
        await viewModel.RefreshAsync();

        await viewModel.NewMaterialCommand.ExecuteAsync(null);

        Assert.NotNull(dialogs.LastContext);
        Assert.Null(dialogs.LastContext!.Material);
        Assert.Single(api.Materials);
        Assert.Equal("MAT-NEW", api.Materials[0].Code);
    }

    [Fact]
    public async Task OpenMaterial_OpensSelectedAndUpdates()
    {
        var api = new FakeApiClient();
        var equipment = AddEquipment(api);
        var material = AddMaterial(api, equipment);
        var dialogs = new FakeMaterialDialogService
        {
            NextResult = new MaterialEditResult(
                equipment.Id, material.Code, "更新后的材料", "Φ16×2", "304SS", "米", 99m, "updated")
        };
        var viewModel = CreateViewModel(api, dialogs);
        await viewModel.RefreshAsync();
        viewModel.SelectedMaterial = viewModel.Materials[0];

        await viewModel.OpenMaterialCommand.ExecuteAsync(null);

        Assert.Same(material, dialogs.LastContext!.Material);
        Assert.Equal("更新后的材料", api.Materials[0].Name);
        Assert.Equal(99m, api.Materials[0].Quantity);
    }

    [Fact]
    public async Task DeleteMaterial_ConfirmsAndDeletes()
    {
        var api = new FakeApiClient();
        var equipment = AddEquipment(api);
        AddMaterial(api, equipment);
        var dialogs = new FakeMaterialDialogService { ConfirmResult = true };
        var viewModel = CreateViewModel(api, dialogs);
        await viewModel.RefreshAsync();
        viewModel.SelectedMaterial = viewModel.Materials[0];

        await viewModel.DeleteMaterialCommand.ExecuteAsync(null);

        Assert.Empty(api.Materials);
        Assert.Empty(viewModel.Materials);
    }

    [Fact]
    public async Task DeleteMaterial_Cancel_LeavesMaterial()
    {
        var api = new FakeApiClient();
        var equipment = AddEquipment(api);
        AddMaterial(api, equipment);
        var dialogs = new FakeMaterialDialogService { ConfirmResult = false };
        var viewModel = CreateViewModel(api, dialogs);
        await viewModel.RefreshAsync();
        viewModel.SelectedMaterial = viewModel.Materials[0];

        await viewModel.DeleteMaterialCommand.ExecuteAsync(null);

        Assert.Single(api.Materials);
        Assert.Single(viewModel.Materials);
    }

    [Fact]
    public async Task NewMaterial_WhenActiveFilterHidesSavedRow_ResetsFilterAndSelects()
    {
        var api = new FakeApiClient();
        AddEquipment(api);
        var dialogs = new FakeMaterialDialogService
        {
            NextResult = new MaterialEditResult(
                null, "MAT-NEW", "新电缆", null, null, "米", 5m, null)
        };
        var viewModel = CreateViewModel(api, dialogs);
        await viewModel.RefreshAsync();
        viewModel.SearchText = "does-not-match";

        await viewModel.NewMaterialCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.NotNull(viewModel.SelectedMaterial);
        Assert.Contains(viewModel.Materials, x => x.Code == "MAT-NEW");
    }

    [Fact]
    public async Task Viewer_CanOpenReadOnlyButCannotWrite()
    {
        var api = new FakeApiClient();
        var equipment = AddEquipment(api);
        AddMaterial(api, equipment);
        var dialogs = new FakeMaterialDialogService();
        var viewModel = CreateViewModel(api, dialogs, roles: new[] { "Viewer" });
        await viewModel.RefreshAsync();
        viewModel.SelectedMaterial = viewModel.Materials[0];

        await viewModel.OpenMaterialCommand.ExecuteAsync(null);
        Assert.NotNull(dialogs.LastContext);
        Assert.True(dialogs.LastContext!.IsReadOnly);

        await viewModel.NewMaterialCommand.ExecuteAsync(null);
        await viewModel.DeleteMaterialCommand.ExecuteAsync(null);

        Assert.False(viewModel.CanWrite);
        Assert.Contains("权限", viewModel.ErrorMessage);
        Assert.Single(api.Materials);
    }

    private static MaterialIndexViewModel CreateViewModel(
        FakeApiClient api,
        FakeMaterialDialogService? dialogs = null,
        string[]? roles = null)
    {
        var context = new ProjectContext { CurrentProject = api.Project };
        return new MaterialIndexViewModel(api.CreateSession(roles), context, dialogs);
    }

    private static EquipmentDto AddEquipment(FakeApiClient api)
    {
        var equipment = new EquipmentDto
        {
            Id = Guid.NewGuid(),
            PlantId = api.Plant.Id,
            ProjectId = api.Project.Id,
            TagNo = "PT-1001",
            Name = "压力变送器",
            Discipline = "Instrument",
            Quantity = 1,
            CreatedAt = DateTime.UtcNow
        };
        api.Equipments.Add(equipment);
        return equipment;
    }

    private static MaterialItemDto AddMaterial(FakeApiClient api, EquipmentDto equipment)
    {
        var material = new MaterialItemDto
        {
            Id = Guid.NewGuid(),
            ProjectId = api.Project.Id,
            EquipmentId = equipment.Id,
            EquipmentTagNo = equipment.TagNo,
            EquipmentName = equipment.Name,
            Code = "MAT-001",
            Name = "不锈钢管",
            Spec = "Φ14×2",
            Material = "316SS",
            Unit = "米",
            Quantity = 120m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        api.Materials.Add(material);
        return material;
    }

    private sealed class FakeMaterialDialogService : IMaterialDialogService
    {
        public MaterialEditorContext? LastContext { get; private set; }
        public MaterialEditResult? NextResult { get; set; }
        public bool ConfirmResult { get; set; } = true;

        public MaterialEditResult? EditMaterial(MaterialEditorContext context)
        {
            LastContext = context;
            return NextResult;
        }

        public bool Confirm(string message) => ConfirmResult;
    }
}
