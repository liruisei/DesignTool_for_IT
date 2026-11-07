using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Equipment;

namespace WinDesign.Client.Tests;

public class MainViewModelTests
{
    [Fact]
    public async Task ReactivatingIndexDocument_RefreshesEquipment()
    {
        var api = new FakeApiClient();
        api.Equipments.Add(CreateEquipment(api.Project.Id, "PT-1001"));
        var viewModel = new MainViewModel(new SessionService(api));
        for (var i = 0; i < 50 && viewModel.Projects.Count == 0; i++)
        {
            await Task.Delay(10);
        }

        viewModel.OpenIndexCommand.Execute(null);
        var indexTab = viewModel.Documents.Single(x => x.Key == "index");
        var indexViewModel = (EquipmentIndexViewModel)indexTab.ViewModel;
        await indexViewModel.RefreshAsync();
        Assert.Single(indexViewModel.Equipments);

        api.Equipments.Add(CreateEquipment(api.Project.Id, "PT-2002"));
        viewModel.OpenPbsCommand.Execute(null);
        viewModel.OpenIndexCommand.Execute(null);
        await Task.Delay(50);

        Assert.Contains(indexViewModel.Equipments, x => x.TagNo == "PT-2002");
    }

    private static EquipmentDto CreateEquipment(Guid projectId, string tagNo) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = projectId,
        TagNo = tagNo,
        Name = "压力变送器",
        Discipline = "Instrument",
        Status = "Planned",
        Quantity = 1,
        CreatedAt = DateTime.UtcNow
    };
}
