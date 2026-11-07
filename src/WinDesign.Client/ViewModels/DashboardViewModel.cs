using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WinDesign.Client.Services;
using WinDesign.Contracts.Projects;

namespace WinDesign.Client.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly SessionService _session;

    [ObservableProperty]
    private int userCount;

    [ObservableProperty]
    private int projectCount;

    [ObservableProperty]
    private int dictionaryTypeCount;

    [ObservableProperty]
    private int equipmentCount;

    public ObservableCollection<ProjectDto> RecentProjects { get; } = new();

    public DashboardViewModel(SessionService session)
    {
        _session = session;
        Title = "工作台";
        _ = RefreshAsync();
    }

    public override Task RefreshAsync() => RunAsync(async () =>
    {
        var dashboard = await _session.Api.GetDashboardAsync();
        UserCount = dashboard.UserCount;
        ProjectCount = dashboard.ProjectCount;
        DictionaryTypeCount = dashboard.DictionaryTypeCount;
        EquipmentCount = dashboard.EquipmentCount;
        RecentProjects.Clear();
        foreach (var project in dashboard.RecentProjects)
        {
            RecentProjects.Add(project);
        }
    });
}
