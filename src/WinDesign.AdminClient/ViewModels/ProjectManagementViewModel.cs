using WinDesign.Client.ViewModels;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Contracts.Plants;
using WinDesign.Contracts.Projects;

namespace WinDesign.AdminClient.ViewModels;

public sealed record StatusOption(string Code, string Name);

public partial class ProjectManagementViewModel : ViewModelBase, IPendingChanges
{
    private readonly SessionService _session;

    public ObservableCollection<ProjectDto> Projects { get; } = new();
    public ObservableCollection<PlantDto> Plants { get; } = new();

    /// <summary>当前工厂下可作为项目根节点的顶层 PBS 节点。</summary>
    public ObservableCollection<PbsNodeOption> PbsRootOptions { get; } = new();

    public IReadOnlyList<StatusOption> StatusOptions { get; } = new[]
    {
        new StatusOption("Active", "进行中"),
        new StatusOption("Archived", "已归档"),
        new StatusOption("Completed", "已完成")
    };

    [ObservableProperty]
    private ProjectDto? selectedProject;

    [ObservableProperty]
    private PlantDto? selectedPlant;

    [ObservableProperty]
    private PbsNodeOption? selectedPbsRoot;

    /// <summary>保存时使用的根节点 Id；选择项目时同步填充，避免根节点下拉异步加载期间丢失。</summary>
    [ObservableProperty]
    private Guid? editPbsRootNodeId;

    private int _pbsRootLoadVersion;

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
    private string editLocation = string.Empty;

    [ObservableProperty]
    private DateTime? editStartDate;

    [ObservableProperty]
    private DateTime? editEndDate;

    [ObservableProperty]
    private string editPhase = "详细设计";

    [ObservableProperty]
    private string editStatus = "Active";

    [ObservableProperty]
    private string editDescription = string.Empty;

    [ObservableProperty]
    private string? statusMessage;

    public string PlantName => IsManagementMode ? "全部工厂" : _session.Plant?.Name ?? "未选择工厂";
    public bool IsManagementMode => _session.IsManagementMode;
    /// <summary>管理模块新建项目时需要选择所属工厂；编辑已有项目不允许改工厂。</summary>
    public bool CanSelectPlant => IsManagementMode && IsCreating;

    public ProjectManagementViewModel(SessionService session)
    {
        _session = session;
        Title = "项目配置";
        _ = RefreshAsync();
    }

    public override Task RefreshAsync() => RunAsync(async () =>
    {
        if (IsManagementMode)
        {
            await LoadPlantsAsync();
        }
        await LoadProjectsAsync();
    });

    private async Task LoadPlantsAsync()
    {
        var plants = await _session.Api.GetPlantsAsync();
        var currentId = SelectedPlant?.Id;
        Plants.Clear();
        foreach (var plant in plants)
        {
            Plants.Add(plant);
        }
        SelectedPlant = Plants.FirstOrDefault(x => x.Id == currentId)
                        ?? Plants.FirstOrDefault();
    }

    [RelayCommand]
    private Task SearchAsync() => RunAsync(LoadProjectsAsync);

    /// <summary>编辑已有项目时自动保存；新建未保存的项目不自动创建。</summary>
    public async Task SavePendingAsync()
    {
        if (IsCreating || SelectedProject is null)
        {
            return;
        }

        await SaveAsync();
        if (!string.IsNullOrWhiteSpace(ErrorMessage))
        {
            throw new ApiException(ErrorMessage);
        }
    }

    [RelayCommand]
    private void NewProject()
    {
        SelectedProject = null;
        IsCreating = true;
        EditorTitle = "新建项目";
        OnPropertyChanged(nameof(CanSelectPlant));
        EditCode = string.Empty;
        EditName = string.Empty;
        EditClient = string.Empty;
        EditLocation = string.Empty;
        EditStartDate = null;
        EditEndDate = null;
        EditPhase = "详细设计";
        EditStatus = "Active";
        EditDescription = string.Empty;
        SelectedPbsRoot = null;
        EditPbsRootNodeId = null;
        _ = LoadPbsRootOptionsAsync(SelectedPlant?.Id ?? _session.PlantId, null);
        StatusMessage = null;
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (EditStartDate is not null && EditEndDate is not null && EditEndDate < EditStartDate)
        {
            throw new ApiException("计划完成日期不能早于开工日期");
        }
        if (EditPbsRootNodeId is null)
        {
            throw new ApiException("请选择项目所在的 PBS 根节点");
        }

        // 记录保存开始时正在编辑的项目，保存完成后若用户已切换选择则不要抢回。
        var editedProjectId = SelectedProject?.Id;
        ProjectDto saved;
        if (IsCreating)
        {
            if (string.IsNullOrWhiteSpace(EditCode) || string.IsNullOrWhiteSpace(EditName))
            {
                throw new ApiException("项目代号和项目名称不能为空");
            }

            if (IsManagementMode && SelectedPlant is null)
            {
                throw new ApiException("请选择项目所属工厂");
            }

            saved = await _session.Api.CreateProjectAsync(new CreateProjectRequest
            {
                PlantId = IsManagementMode ? SelectedPlant?.Id : null,
                Code = EditCode.Trim(),
                Name = EditName.Trim(),
                ClientName = NullIfEmpty(EditClient),
                Location = NullIfEmpty(EditLocation),
                Phase = NullIfEmpty(EditPhase),
                StartDate = EditStartDate,
                EndDate = EditEndDate,
                Description = NullIfEmpty(EditDescription),
                PbsRootNodeId = EditPbsRootNodeId
            });
            StatusMessage = "项目已创建";
        }
        else
        {
            if (SelectedProject is null)
            {
                throw new ApiException("请选择项目");
            }

            saved = await _session.Api.UpdateProjectAsync(SelectedProject.Id, new UpdateProjectRequest
            {
                Name = EditName.Trim(),
                ClientName = NullIfEmpty(EditClient),
                Location = NullIfEmpty(EditLocation),
                Phase = NullIfEmpty(EditPhase),
                StartDate = EditStartDate,
                EndDate = EditEndDate,
                Status = EditStatus,
                Description = NullIfEmpty(EditDescription),
                PbsRootNodeId = EditPbsRootNodeId
            });
            StatusMessage = "项目已保存";
        }

        await LoadProjectsAsync();
        if (SelectedProject is null || SelectedProject.Id == editedProjectId)
        {
            SelectedProject = Projects.FirstOrDefault(x => x.Id == saved.Id);
        }
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
        OnPropertyChanged(nameof(CanSelectPlant));
        EditCode = value.Code;
        EditName = value.Name;
        EditClient = value.ClientName ?? string.Empty;
        EditLocation = value.Location ?? string.Empty;
        EditStartDate = value.StartDate;
        EditEndDate = value.EndDate;
        EditPhase = value.Phase ?? string.Empty;
        EditStatus = value.Status;
        EditDescription = value.Description ?? string.Empty;
        EditPbsRootNodeId = value.PbsRootNodeId;
        _ = ApplySelectedProjectAsync(value);
        StatusMessage = null;
    }

    partial void OnSelectedPbsRootChanged(PbsNodeOption? value) =>
        EditPbsRootNodeId = value?.Id;

    /// <summary>新建项目时切换工厂，根节点选项随之刷新；编辑已有项目不允许改工厂。</summary>
    partial void OnSelectedPlantChanged(PlantDto? value)
    {
        if (!IsCreating)
        {
            return;
        }

        EditPbsRootNodeId = null;
        _ = LoadPbsRootOptionsAsync(value?.Id ?? _session.PlantId, null);
    }

    /// <summary>选中已有项目时同步其工厂，并加载该工厂的顶层 PBS 节点。</summary>
    private async Task ApplySelectedProjectAsync(ProjectDto project)
    {
        if (IsManagementMode &&
            Plants.FirstOrDefault(x => x.Id == project.PlantId) is { } plant &&
            SelectedPlant?.Id != plant.Id)
        {
            SelectedPlant = plant;
        }

        await LoadPbsRootOptionsAsync(project.PlantId, project.PbsRootNodeId);
    }

    /// <summary>
    /// 加载工厂顶层 PBS 节点作为项目根节点候选。版本号用于丢弃过期的异步结果。
    /// </summary>
    private async Task LoadPbsRootOptionsAsync(Guid? plantId, Guid? selectedId)
    {
        var version = ++_pbsRootLoadVersion;
        var options = new List<PbsNodeOption>();
        try
        {
            if (plantId is Guid id)
            {
                var nodes = await _session.Api.GetPbsNodesAsync(id);
                options.AddRange(nodes
                    .Where(x => x.ParentId is null)
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new PbsNodeOption(x.Id, x.Code + "  " + x.Name)));
            }
        }
        catch (Exception ex)
        {
            if (version == _pbsRootLoadVersion)
            {
                ErrorMessage = ex.Message;
            }
            return;
        }

        if (version != _pbsRootLoadVersion)
        {
            return;
        }

        PbsRootOptions.Clear();
        foreach (var option in options)
        {
            PbsRootOptions.Add(option);
        }
        SelectedPbsRoot = PbsRootOptions.FirstOrDefault(x => x.Id == selectedId);
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
