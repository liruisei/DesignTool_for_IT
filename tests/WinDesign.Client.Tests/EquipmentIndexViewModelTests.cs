using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Dictionaries;
using WinDesign.Contracts.Equipment;
using WinDesign.Contracts.Pbs;
using WinDesign.Contracts.Projects;

namespace WinDesign.Client.Tests;

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
    public async Task Save_WithEmptyTag_SetsError()
    {
        var (api, project, _) = CreateData();
        var viewModel = CreateViewModel(api, project);
        await viewModel.RefreshAsync();
        viewModel.NewEquipmentCommand.Execute(null);
        viewModel.EditTagNo = string.Empty;
        viewModel.EditName = "设备";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Contains("不能为空", viewModel.ErrorMessage);
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

    private static EquipmentIndexViewModel CreateViewModel(FakeApiClient api, ProjectDto project)
    {
        var session = new SessionService(api);
        var context = new ProjectContext { CurrentProject = project };
        return new EquipmentIndexViewModel(session, context);
    }
}
