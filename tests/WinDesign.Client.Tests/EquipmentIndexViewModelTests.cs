using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Projects;

namespace WinDesign.Client.Tests;

public sealed class FakeEquipmentDialogService : IEquipmentDialogService
{
    public EquipmentEditorContext? LastContext { get; private set; }
    public EquipmentUpsertRequest? NextResult { get; set; }
    public bool ConfirmResult { get; set; } = true;

    public EquipmentUpsertRequest? EditEquipment(EquipmentEditorContext context)
    {
        LastContext = context;
        return NextResult;
    }

    public bool Confirm(string message) => ConfirmResult;
}

public class EquipmentIndexViewModelTests
{
    [Fact]
    public async Task Refresh_LoadsEquipmentsTypesAndPbsOptions()
    {
        var (api, project, _) = CreateData();
        var viewModel = CreateViewModel(api, project);

        await viewModel.RefreshAsync();

        Assert.Single(viewModel.Equipments);
        Assert.Single(viewModel.EquipmentTypes);
        Assert.Equal(2, viewModel.PbsEditorOptions.Count);
    }

    [Fact]
    public async Task Search_FiltersByDiscipline()
    {
        var (api, project, _) = CreateData();
        var viewModel = CreateViewModel(api, project);
        await viewModel.RefreshAsync();

        viewModel.FilterDiscipline = "Telecom";
        await viewModel.SearchCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.Equipments);
    }

    [Fact]
    public async Task NewEquipment_OpensDialogAndCreates()
    {
        var (api, project, root) = CreateData();
        var dialogs = new FakeEquipmentDialogService
        {
            NextResult = new EquipmentUpsertRequest
            {
                PbsNodeId = root.Id,
                TagNo = "PT-2001",
                Name = "新压力变送器",
                Discipline = "Instrument",
                Status = "Planned",
                Quantity = 1
            }
        };
        var viewModel = CreateViewModel(api, project, dialogs);
        await viewModel.RefreshAsync();

        await viewModel.NewEquipmentCommand.ExecuteAsync(null);

        Assert.NotNull(dialogs.LastContext);
        Assert.Null(dialogs.LastContext!.Equipment);
        Assert.Contains(api.Equipments, x => x.TagNo == "PT-2001");
    }

    [Fact]
    public async Task EditEquipment_OpensSelectedAndUpdates()
    {
        var (api, project, _) = CreateData();
        var dialogs = new FakeEquipmentDialogService
        {
            NextResult = new EquipmentUpsertRequest
            {
                TagNo = "PT-1001",
                LoopNo = "LIC-1001",
                Name = "改名后的设备",
                Discipline = "Instrument",
                Status = "Designed",
                Quantity = 2
            }
        };
        var viewModel = CreateViewModel(api, project, dialogs);
        await viewModel.RefreshAsync();
        viewModel.SelectedEquipment = viewModel.Equipments[0];

        await viewModel.EditEquipmentCommand.ExecuteAsync(null);

        Assert.NotNull(dialogs.LastContext!.Equipment);
        Assert.Equal("改名后的设备", api.Equipments.Single(x => x.TagNo == "PT-1001").Name);
    }

    [Fact]
    public async Task DuplicateEquipment_ClearsTagAndCreates()
    {
        var (api, project, _) = CreateData();
        var dialogs = new FakeEquipmentDialogService
        {
            NextResult = new EquipmentUpsertRequest
            {
                TagNo = "PT-1002",
                Name = "压力变送器",
                Discipline = "Instrument",
                Status = "Planned",
                Quantity = 1
            }
        };
        var viewModel = CreateViewModel(api, project, dialogs);
        await viewModel.RefreshAsync();
        viewModel.SelectedEquipment = viewModel.Equipments[0];

        await viewModel.DuplicateEquipmentCommand.ExecuteAsync(null);

        Assert.True(dialogs.LastContext!.IsCopy);
        Assert.Contains(api.Equipments, x => x.TagNo == "PT-1002");
    }

    [Fact]
    public async Task DeleteEquipment_ConfirmsAndDeletes()
    {
        var (api, project, _) = CreateData();
        var dialogs = new FakeEquipmentDialogService { ConfirmResult = true };
        var viewModel = CreateViewModel(api, project, dialogs);
        await viewModel.RefreshAsync();
        viewModel.SelectedEquipment = viewModel.Equipments[0];

        await viewModel.DeleteEquipmentCommand.ExecuteAsync(null);

        Assert.Empty(api.Equipments);
    }

    private static (FakeApiClient Api, ProjectDto Project, PbsNodeDto Root) CreateData()
    {
        var api = new FakeApiClient();
        var project = api.Project;
        var root = new PbsNodeDto(Guid.NewGuid(), project.Id, null, "ROOT", "全厂", "Root", 0, null, 0, 1);
        api.PbsNodes.Add(root);
        api.EquipmentTypes.Add(new DictionaryItemDto(
            Guid.NewGuid(), Guid.NewGuid(), "EQUIPMENT_TYPE", "PT", "压力变送器", 1, true, null));
        api.Equipments.Add(new EquipmentDto
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            PbsNodeId = root.Id,
            PbsNodeName = root.Name,
            TagNo = "PT-1001",
            LoopNo = "LIC-1001",
            Name = "压力变送器",
            TypeCode = "PT",
            Discipline = "Instrument",
            Status = "Planned",
            Location = "A 区",
            Quantity = 1,
            CreatedAt = DateTime.UtcNow
        });
        return (api, project, root);
    }

    private static EquipmentIndexViewModel CreateViewModel(
        FakeApiClient api,
        ProjectDto project,
        IEquipmentDialogService? dialogs = null)
    {
        var session = new SessionService(api);
        var context = new ProjectContext { CurrentProject = project };
        return new EquipmentIndexViewModel(session, context, dialogs ?? new FakeEquipmentDialogService());
    }
}
