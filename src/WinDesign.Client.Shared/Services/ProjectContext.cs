using CommunityToolkit.Mvvm.ComponentModel;
using WinDesign.Contracts.Projects;

namespace WinDesign.Client.Services;

public partial class ProjectContext : ObservableObject
{
    [ObservableProperty]
    private ProjectDto? currentProject;

    [ObservableProperty]
    private bool hasProject;

    public event EventHandler? ProjectChanged;

    partial void OnCurrentProjectChanged(ProjectDto? value)
    {
        HasProject = value is not null;
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }
}
