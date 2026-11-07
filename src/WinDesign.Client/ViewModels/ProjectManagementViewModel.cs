using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Contracts.Projects;

namespace WinDesign.Client.ViewModels;

public sealed record StatusOption(string Code, string Name);

public partial class ProjectManagementViewModel : ViewModelBase
{
    private readonly SessionService _session;

    public ObservableCollection<ProjectDto> Projects { get; } = new();

    public IReadOnlyList<StatusOption> StatusOptions { get; } = new[]
    {
        new StatusOption("Active", "进行中"),
        new StatusOption("Archived", "已归档"),
        new StatusOption("Completed", "已完成")
    };

    [ObservableProperty]
    private ProjectDto? selectedProject;

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string editorTitle = "新建项目";

    [ObservableProperty]
    private bool isCreating = true;

    [ObservableProperty]
    private string editCode = string.Empty;

    [ObservableProperty]
    private string editName = string.Empty;

    [ObservableProperty]
    private string editClient = string.Empty;

    [ObservableProperty]
    private string editPhase = "详细设计";

    [ObservableProperty]
    private string editStatus = "Active";

    [ObservableProperty]
    private string editDescription = string.Empty;

    [ObservableProperty]
    private string? statusMessage;

    public ProjectManagementViewModel(SessionService session)
    {
        _session = session;
        Title = "项目管理";
        _ = RefreshAsync();
    }

    public override Task RefreshAsync() => RunAsync(LoadProjectsAsync);

    [RelayCommand]
    private Task SearchAsync() => RunAsync(LoadProjectsAsync);

    [RelayCommand]
    private void NewProject()
    {
        SelectedProject = null;
        IsCreating = true;
        EditorTitle = "新建项目";
        EditCode = string.Empty;
        EditName = string.Empty;
        EditClient = string.Empty;
        EditPhase = "详细设计";
        EditStatus = "Active";
        EditDescription = string.Empty;
        StatusMessage = null;
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        ProjectDto saved;
        if (IsCreating)
        {
            if (string.IsNullOrWhiteSpace(EditCode) || string.IsNullOrWhiteSpace(EditName))
            {
                throw new ApiException("项目代号和项目名称不能为空");
            }

            saved = await _session.Api.CreateProjectAsync(new CreateProjectRequest(
                EditCode.Trim(),
                EditName.Trim(),
                NullIfEmpty(EditClient),
                NullIfEmpty(EditPhase),
                NullIfEmpty(EditDescription)));
            StatusMessage = "项目已创建";
        }
        else
        {
            if (SelectedProject is null)
            {
                throw new ApiException("请选择项目");
            }

            saved = await _session.Api.UpdateProjectAsync(SelectedProject.Id, new UpdateProjectRequest(
                EditName.Trim(),
                NullIfEmpty(EditClient),
                NullIfEmpty(EditPhase),
                EditStatus,
                NullIfEmpty(EditDescription)));
            StatusMessage = "项目已保存";
        }

        await LoadProjectsAsync();
        SelectedProject = Projects.FirstOrDefault(x => x.Id == saved.Id);
    });

    [RelayCommand]
    private Task ArchiveAsync() => RunAsync(async () =>
    {
        if (SelectedProject is null)
        {
            throw new ApiException("请选择项目");
        }

        await _session.Api.ArchiveProjectAsync(SelectedProject.Id);
        StatusMessage = "项目已归档";
        await LoadProjectsAsync();
    });

    partial void OnSelectedProjectChanged(ProjectDto? value)
    {
        if (value is null)
        {
            return;
        }

        IsCreating = false;
        EditorTitle = "编辑项目：" + value.Code;
        EditCode = value.Code;
        EditName = value.Name;
        EditClient = value.ClientName ?? string.Empty;
        EditPhase = value.Phase ?? string.Empty;
        EditStatus = value.Status;
        EditDescription = value.Description ?? string.Empty;
        StatusMessage = null;
    }

    private async Task LoadProjectsAsync()
    {
        var result = await _session.Api.GetProjectsAsync(SearchText);
        var currentId = SelectedProject?.Id;
        Projects.Clear();
        foreach (var project in result.Items)
        {
            Projects.Add(project);
        }
        if (currentId is not null)
        {
            SelectedProject = Projects.FirstOrDefault(x => x.Id == currentId);
        }
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
