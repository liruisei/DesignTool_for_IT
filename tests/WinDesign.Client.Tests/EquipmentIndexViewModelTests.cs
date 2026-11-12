using WinDesign.Client.Services;
using WinDesign.DesignClient.ViewModels;
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

    /// <summary>模拟弹窗内保存失败时显示的错误；有值时弹窗保持打开（返回 null）。</summary>
    public string? LastError { get; private set; }

    public EquipmentUpsertRequest? EditEquipment(
        EquipmentEditorContext context,
        Func<EquipmentUpsertRequest, Task<string?>>? saveAsync = null)
    {
        LastContext = context;
        LastError = null;
        if (NextResult is null)
        {
            return null;
        }

        if (saveAsync is not null)
        {
            var error = saveAsync(NextResult).GetAwaiter().GetResult();
            if (!string.IsNullOrWhiteSpace(error))
            {
                LastError = error;
                return null;
            }
        }

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

    [Fact]
    public async Task NewEquipment_WithDuplicateTag_ShowsErrorInDialogAndDoesNotCreate()
    {
        var (api, project, root) = CreateData();
        var dialogs = new FakeEquipmentDialogService
        {
            NextResult = new EquipmentUpsertRequest
            {
                PbsNodeId = root.Id,
                TagNo = "pt-1001", // 已存在 PT-1001，大小写不同也应拒绝
                Name = "重复设备",
                Discipline = "Instrument",
                Quantity = 1
            }
        };
        var viewModel = CreateViewModel(api, project, dialogs);
        await viewModel.RefreshAsync();

        await viewModel.NewEquipmentCommand.ExecuteAsync(null);

        Assert.Contains("位号已存在", dialogs.LastError);
        Assert.Single(api.Equipments);
    }

    [Fact]
    public async Task DuplicateEquipment_WithDuplicateTag_ShowsErrorInDialogAndDoesNotCreate()
    {
        var (api, project, _) = CreateData();
        var dialogs = new FakeEquipmentDialogService
        {
            NextResult = new EquipmentUpsertRequest
            {
                TagNo = "PT-1001",
                Name = "复制后的设备",
                Discipline = "Instrument",
                Quantity = 1
            }
        };
        var viewModel = CreateViewModel(api, project, dialogs);
        await viewModel.RefreshAsync();
        viewModel.SelectedEquipment = viewModel.Equipments[0];

        await viewModel.DuplicateEquipmentCommand.ExecuteAsync(null);

        Assert.Contains("位号已存在", dialogs.LastError);
        Assert.Single(api.Equipments);
    }

    [Fact]
    public async Task UpdateSelection_WithMultipleRows_DisablesSingleRowActions()
    {
        var (api, project, root) = CreateData();
        api.Equipments.Add(new EquipmentDto
        {
            Id = Guid.NewGuid(),
            PlantId = api.Plant.Id,
            ProjectId = project.Id,
            PbsNodeId = root.Id,
            TagNo = "PT-1002",
            Name = "第二台压力变送器",
            Discipline = "Instrument",
            Quantity = 1,
            CreatedAt = DateTime.UtcNow
        });
        var viewModel = CreateViewModel(api, project);
        await viewModel.RefreshAsync();

        viewModel.UpdateSelection(viewModel.Equipments.ToList());

        Assert.Equal(2, viewModel.SelectedEquipments.Count);
        Assert.True(viewModel.IsMultipleSelection);
        Assert.False(viewModel.CanCreateNew);
        Assert.False(viewModel.CanRefresh);
        Assert.False(viewModel.CanCreateSpec);
        Assert.True(viewModel.HasSelection);
    }

    [Fact]
    public async Task DeleteEquipment_WithMultipleSelected_DeletesAll()
    {
        var (api, project, root) = CreateData();
        api.Equipments.Add(new EquipmentDto
        {
            Id = Guid.NewGuid(),
            PlantId = api.Plant.Id,
            ProjectId = project.Id,
            PbsNodeId = root.Id,
            TagNo = "PT-1002",
            Name = "第二台压力变送器",
            Discipline = "Instrument",
            Quantity = 1,
            CreatedAt = DateTime.UtcNow
        });
        var dialogs = new FakeEquipmentDialogService { ConfirmResult = true };
        var viewModel = CreateViewModel(api, project, dialogs);
        await viewModel.RefreshAsync();
        viewModel.UpdateSelection(viewModel.Equipments.ToList());

        await viewModel.DeleteEquipmentCommand.ExecuteAsync(null);

        Assert.Empty(api.Equipments);
        Assert.False(viewModel.HasSelection);
    }

    [Fact]
    public async Task CreateSpec_CreatesBlankSpecForSelectedEquipment()
    {
        var (api, project, _) = CreateData();
        var viewModel = CreateViewModel(api, project);
        await viewModel.RefreshAsync();
        viewModel.SelectedEquipment = viewModel.Equipments[0];

        await viewModel.CreateSpecCommand.ExecuteAsync(null);

        var spec = Assert.Single(api.EquipmentSpecs);
        Assert.Equal("PT-1001-SP", spec.SpecCode);
        Assert.Equal("PT-1001 规格书", spec.SpecName);
        Assert.Empty(spec.Parameters!);
        Assert.Contains("创建空白规格书", viewModel.StatusMessage);
    }

    [Fact]
    public async Task CreateSpec_WhenEquipmentAlreadyHasSpec_ShowsError()
    {
        var (api, project, _) = CreateData();
        var viewModel = CreateViewModel(api, project);
        await viewModel.RefreshAsync();
        viewModel.SelectedEquipment = viewModel.Equipments[0];

        await viewModel.CreateSpecCommand.ExecuteAsync(null);
        await viewModel.CreateSpecCommand.ExecuteAsync(null);

        Assert.Single(api.EquipmentSpecs);
        Assert.Contains("已有规格书", viewModel.ErrorMessage);
    }

    private static (FakeApiClient Api, ProjectDto Project, PbsNodeDto Root) CreateData()
    {
        var api = new FakeApiClient();
        var project = api.Project;
        var root = new PbsNodeDto(Guid.NewGuid(), api.Plant.Id, null, "AREA-A", "A 区", "Area", 0, null, 0, 1);
        api.PbsNodes.Add(root);
        api.EquipmentTypes.Add(new DictionaryItemDto(
            Guid.NewGuid(), Guid.NewGuid(), "EQUIPMENT_TYPE", "PT", "压力变送器", 1, true, null));
        api.Equipments.Add(new EquipmentDto
        {
            Id = Guid.NewGuid(),
            PlantId = api.Plant.Id,
            ProjectId = project.Id,
            PbsNodeId = root.Id,
            PbsNodeName = root.Name,
            TagNo = "PT-1001",
            LoopNo = "LIC-1001",
            Name = "压力变送器",
            TypeCode = "PT",
            Discipline = "Instrument",
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
        var session = api.CreateSession();
        var context = new ProjectContext { CurrentProject = project };
        return new EquipmentIndexViewModel(session, context, dialogs ?? new FakeEquipmentDialogService());
    }
}
