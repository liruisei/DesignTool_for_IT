using WinDesign.Client.ViewModels;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Contracts.Plants;
using WinDesign.Contracts.Projects;
using WinDesign.Contracts.Users;

namespace WinDesign.AdminClient.ViewModels;

public sealed record StatusOption(string Code, string Name);

/// <summary>授权用户清单中的一行：用户名 + 勾选状态。</summary>
public partial class UserSelectionItem : ObservableObject
{
    public UserSelectionItem(UserDto user)
    {
        User = user;
    }

    public UserDto User { get; }
    public Guid UserId => User.Id;
    public string UserName => User.UserName;
    public string DisplayName => User.DisplayName;

    [ObservableProperty]
    private bool isSelected;
}

public partial class ProjectManagementViewModel : ViewModelBase, IPendingChanges
{
    private readonly SessionService _session;

    public ObservableCollection<ProjectDto> Projects { get; } = new();
    public ObservableCollection<PlantDto> Plants { get; } = new();

    /// <summary>项目根节点候选：工厂根节点（全厂）及工厂下的顶层 PBS 节点。</summary>
    public ObservableCollection<PbsNodeOption> PbsRootOptions { get; } = new();

    /// <summary>根节点下拉中的「工厂根节点」选项：Id 为空表示项目范围为整个工厂。</summary>
    public const string FactoryRootDisplay = "工厂根节点（全厂）";

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

    // ---------- 项目成员 ----------

    public ObservableCollection<ProjectMemberDto> Members { get; } = new();

    /// <summary>授权用户清单：启用状态的用户，勾选后统一授予所选项目角色。</summary>
    public ObservableCollection<UserSelectionItem> UserSelectionItems { get; } = new();

    public IReadOnlyList<ProjectRoleOptionDto> MemberRoleOptions => ProjectRoleCatalog.Options;

    private readonly List<UserDto> _allUsers = new();

    [ObservableProperty]
    private ProjectMemberDto? selectedMember;

    [ObservableProperty]
    private string editMemberRole = ProjectRoleCatalog.Designer;

    /// <summary>批量授权时使用的项目角色。</summary>
    [ObservableProperty]
    private string grantRole = ProjectRoleCatalog.Designer;

    /// <summary>只有已保存的项目才能分配成员；新建未保存的项目没有 Id。</summary>
    public bool CanManageMembers => !IsCreating && SelectedProject is not null;

    /// <summary>项目负责人不能被移除或降级，界面据此禁用编辑按钮。</summary>
    public bool CanEditSelectedMember =>
        SelectedMember is not null &&
        SelectedProject is not null &&
        SelectedMember.UserId != SelectedProject.OwnerUserId;

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
        await LoadUsersAsync();
        await LoadProjectsAsync();
    });

    private async Task LoadUsersAsync()
    {
        var result = await _session.Api.GetUsersAsync(null, 1, 200);
        _allUsers.Clear();
        _allUsers.AddRange(result.Items);
        RefreshUserSelectionItems();
    }

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
        SelectedMember = null;
        Members.Clear();
        ClearUserSelection();
        OnPropertyChanged(nameof(CanManageMembers));
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

    // ---------- 项目成员 ----------

    /// <summary>勾选用户 → 选择角色 → 确认授权（批量）。</summary>
    [RelayCommand]
    private Task GrantMembers() => RunAsync(async () =>
    {
        if (SelectedProject is null)
        {
            throw new ApiException("请先选择项目");
        }

        var userIds = UserSelectionItems
            .Where(x => x.IsSelected)
            .Select(x => x.UserId)
            .ToList();
        if (userIds.Count == 0)
        {
            throw new ApiException("请至少勾选一个用户");
        }

        var members = await _session.Api.BulkGrantProjectMembersAsync(
            SelectedProject.Id,
            new BulkGrantProjectMembersRequest(userIds, GrantRole));

        Members.Clear();
        foreach (var member in members)
        {
            Members.Add(member);
        }
        SelectedMember = null;
        ClearUserSelection();
        StatusMessage = $"已授权 {userIds.Count} 个用户；项目角色：{ProjectRoleCatalog.DisplayName(GrantRole)}";
    });

    [RelayCommand]
    private void SelectAllUsers()
    {
        foreach (var item in UserSelectionItems)
        {
            item.IsSelected = true;
        }
    }

    [RelayCommand]
    private void ClearUserSelection()
    {
        foreach (var item in UserSelectionItems)
        {
            item.IsSelected = false;
        }
    }

    [RelayCommand]
    private Task UpdateMemberRole() => RunAsync(async () =>
    {
        if (SelectedProject is null || SelectedMember is null)
        {
            throw new ApiException("请先选择项目成员");
        }

        var updated = await _session.Api.UpdateProjectMemberAsync(
            SelectedProject.Id,
            SelectedMember.UserId,
            new UpsertProjectMemberRequest(SelectedMember.UserId, EditMemberRole));
        var index = Members.IndexOf(SelectedMember);
        if (index >= 0)
        {
            Members[index] = updated;
            SelectedMember = updated;
        }
        StatusMessage = $"已更新 {updated.DisplayName} 的项目角色";
    });

    [RelayCommand]
    private Task RemoveMember() => RunAsync(async () =>
    {
        if (SelectedProject is null || SelectedMember is null)
        {
            throw new ApiException("请先选择项目成员");
        }

        var removed = SelectedMember;
        await _session.Api.RemoveProjectMemberAsync(SelectedProject.Id, removed.UserId);
        Members.Remove(removed);
        SelectedMember = null;
        StatusMessage = $"已移除成员：{removed.DisplayName}";
    });

    /// <summary>打开成员配置弹窗前刷新用户与成员列表，避免显示过期数据。</summary>
    public Task ReloadMembersAsync() => RunAsync(async () =>
    {
        await LoadUsersAsync();
        if (SelectedProject is { } project)
        {
            await LoadMembersAsync(project);
        }
    });

    private async Task LoadMembersAsync(ProjectDto project)
    {
        try
        {
            var members = await _session.Api.GetProjectMembersAsync(project.Id);
            if (SelectedProject?.Id != project.Id)
            {
                // 加载期间已切换到其他项目，丢弃过期结果。
                return;
            }

            Members.Clear();
            foreach (var member in members)
            {
                Members.Add(member);
            }
        }
        catch (Exception ex)
        {
            if (SelectedProject?.Id == project.Id)
            {
                ErrorMessage = "加载项目成员失败：" + ex.Message;
            }
        }
    }

    private void RefreshUserSelectionItems()
    {
        var selectedIds = UserSelectionItems
            .Where(x => x.IsSelected)
            .Select(x => x.UserId)
            .ToHashSet();

        UserSelectionItems.Clear();
        foreach (var user in _allUsers
                     .Where(x => x.IsActive)
                     .OrderBy(x => x.UserName, StringComparer.OrdinalIgnoreCase))
        {
            UserSelectionItems.Add(new UserSelectionItem(user)
            {
                IsSelected = selectedIds.Contains(user.Id)
            });
        }
    }

    partial void OnSelectedMemberChanged(ProjectMemberDto? value)
    {
        EditMemberRole = ProjectRoleCatalog.Normalize(value?.RoleInProject);
        OnPropertyChanged(nameof(CanEditSelectedMember));
    }

    partial void OnSelectedProjectChanged(ProjectDto? value)
    {
        if (value is null)
        {
            return;
        }

        IsCreating = false;
        EditorTitle = "编辑项目：" + value.Code;
        OnPropertyChanged(nameof(CanSelectPlant));
        OnPropertyChanged(nameof(CanManageMembers));
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
        SelectedMember = null;
        Members.Clear();
        _ = ApplySelectedProjectAsync(value);
        _ = LoadMembersAsync(value);
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
    /// 加载项目根节点候选：首个选项是「工厂根节点（全厂）」，其后是工厂顶层 PBS 节点。
    /// 版本号用于丢弃过期的异步结果。
    /// </summary>
    private async Task LoadPbsRootOptionsAsync(Guid? plantId, Guid? selectedId)
    {
        var version = ++_pbsRootLoadVersion;
        var options = new List<PbsNodeOption>();
        try
        {
            if (plantId is Guid id)
            {
                // Id 为空代表工厂本身作为项目根节点，项目范围为全厂。
                options.Add(new PbsNodeOption(null, FactoryRootDisplay));
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
