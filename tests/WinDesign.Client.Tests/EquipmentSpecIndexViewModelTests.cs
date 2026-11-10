using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Equipment;
using WinDesign.DesignClient.ViewModels;

namespace WinDesign.Client.Tests;

public class EquipmentSpecIndexViewModelTests
{
    [Fact]
    public async Task Refresh_LoadsSpecsAndEquipmentFilter()
    {
        var api = new FakeApiClient();
        var equipment = AddEquipment(api);
        AddSpec(api, equipment);
        var viewModel = CreateViewModel(api);

        await viewModel.RefreshAsync();

        Assert.Single(viewModel.Specs);
        Assert.Equal(2, viewModel.EquipmentFilterOptions.Count);
        Assert.Equal("全部设备", viewModel.EquipmentFilterOptions[0].Display);
        Assert.True(viewModel.CanWrite);
    }

    [Fact]
    public async Task NewSpec_OpensDialogAndCreates()
    {
        var api = new FakeApiClient();
        var equipment = AddEquipment(api);
        var dialogs = new FakeSpecDialogService
        {
            NextResult = new EquipmentSpecEditResult(
                equipment.Id, "SPEC-NEW", "新建规格书",
                new[] { new SpecParameterDto { Key = "range", Value = "0~1MPa" } })
        };
        var viewModel = CreateViewModel(api, dialogs);
        await viewModel.RefreshAsync();

        await viewModel.NewSpecCommand.ExecuteAsync(null);

        Assert.NotNull(dialogs.LastContext);
        Assert.Null(dialogs.LastContext!.Spec);
        Assert.Single(api.EquipmentSpecs);
        Assert.Equal("SPEC-NEW", api.EquipmentSpecs[0].SpecCode);
        Assert.Single(viewModel.Specs);
    }

    [Fact]
    public async Task OpenSpec_OpensSelectedAndUpdates()
    {
        var api = new FakeApiClient();
        var equipment = AddEquipment(api);
        var spec = AddSpec(api, equipment);
        var dialogs = new FakeSpecDialogService
        {
            NextResult = new EquipmentSpecEditResult(
                equipment.Id, spec.SpecCode, "更新后的名称",
                new[] { new SpecParameterDto { Key = "range", Value = "0~2.5MPa" } })
        };
        var viewModel = CreateViewModel(api, dialogs);
        await viewModel.RefreshAsync();
        viewModel.SelectedSpec = viewModel.Specs[0];

        await viewModel.OpenSpecCommand.ExecuteAsync(null);

        Assert.Same(spec, dialogs.LastContext!.Spec);
        Assert.Equal("更新后的名称", api.EquipmentSpecs[0].SpecName);
        Assert.Equal("0~2.5MPa", api.EquipmentSpecs[0].Parameters![0].Value);
    }

    [Fact]
    public async Task DeleteSpec_ConfirmsAndDeletes()
    {
        var api = new FakeApiClient();
        var equipment = AddEquipment(api);
        AddSpec(api, equipment);
        var dialogs = new FakeSpecDialogService { ConfirmResult = true };
        var viewModel = CreateViewModel(api, dialogs);
        await viewModel.RefreshAsync();
        viewModel.SelectedSpec = viewModel.Specs[0];

        await viewModel.DeleteSpecCommand.ExecuteAsync(null);

        Assert.Empty(api.EquipmentSpecs);
        Assert.Empty(viewModel.Specs);
    }

    [Fact]
    public async Task NewSpec_WhenActiveFilterHidesSavedRow_ResetsFilterAndSelects()
    {
        var api = new FakeApiClient();
        AddEquipment(api, "PT-1001");
        var other = AddEquipment(api, "PT-2002");
        var dialogs = new FakeSpecDialogService
        {
            NextResult = new EquipmentSpecEditResult(
                other.Id, "SPEC-NEW", "新规格书", Array.Empty<SpecParameterDto>())
        };
        var viewModel = CreateViewModel(api, dialogs);
        await viewModel.RefreshAsync();
        viewModel.SearchText = "does-not-match";

        await viewModel.NewSpecCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.NotNull(viewModel.SelectedSpec);
        Assert.Contains(viewModel.Specs, x => x.SpecCode == "SPEC-NEW");
    }

    [Fact]
    public async Task Viewer_CannotWriteOrOpenEditor()
    {
        var api = new FakeApiClient();
        var equipment = AddEquipment(api);
        AddSpec(api, equipment);
        var dialogs = new FakeSpecDialogService();
        var viewModel = CreateViewModel(api, dialogs, roles: new[] { "Viewer" });
        await viewModel.RefreshAsync();

        await viewModel.NewSpecCommand.ExecuteAsync(null);
        await viewModel.DeleteSpecCommand.ExecuteAsync(null);

        Assert.False(viewModel.CanWrite);
        Assert.True(viewModel.IsReadOnly);
        Assert.Equal(0, dialogs.EditCount);
        Assert.Contains("权限", viewModel.ErrorMessage);
        Assert.Single(api.EquipmentSpecs);
    }

    private static EquipmentSpecIndexViewModel CreateViewModel(
        FakeApiClient api,
        FakeSpecDialogService? dialogs = null,
        string[]? roles = null)
    {
        var context = new ProjectContext { CurrentProject = api.Project };
        return new EquipmentSpecIndexViewModel(api.CreateSession(roles), context, dialogs);
    }

    private static EquipmentDto AddEquipment(FakeApiClient api, string tagNo = "PT-1001")
    {
        var equipment = new EquipmentDto
        {
            Id = Guid.NewGuid(),
            PlantId = api.Plant.Id,
            ProjectId = api.Project.Id,
            TagNo = tagNo,
            Name = "压力变送器",
            Discipline = "Instrument",
            Quantity = 1,
            CreatedAt = DateTime.UtcNow
        };
        api.Equipments.Add(equipment);
        return equipment;
    }

    private static EquipmentSpecDto AddSpec(FakeApiClient api, EquipmentDto equipment)
    {
        var spec = new EquipmentSpecDto
        {
            Id = Guid.NewGuid(),
            EquipmentId = equipment.Id,
            EquipmentTagNo = equipment.TagNo,
            EquipmentName = equipment.Name,
            SpecCode = "SPEC-" + equipment.TagNo,
            SpecName = "压力变送器规格书",
            Parameters = new[] { new SpecParameterDto { Key = "range", Value = "0~1.6MPa" } },
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        api.EquipmentSpecs.Add(spec);
        return spec;
    }

    private sealed class FakeSpecDialogService : IEquipmentSpecDialogService
    {
        public EquipmentSpecEditorContext? LastContext { get; private set; }
        public EquipmentSpecEditResult? NextResult { get; set; }
        public bool ConfirmResult { get; set; } = true;
        public int EditCount { get; private set; }

        public EquipmentSpecEditResult? EditSpec(EquipmentSpecEditorContext context)
        {
            LastContext = context;
            EditCount++;
            return NextResult;
        }

        public bool Confirm(string message) => ConfirmResult;
    }
}
