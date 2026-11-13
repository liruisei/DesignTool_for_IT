using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Client.ViewModels;
using WinDesign.Contracts.Permissions;
using WinDesign.Contracts.Users;

namespace WinDesign.AdminClient.ViewModels;

/// <summary>权限矩阵的一行：一个模块的增删改查勾选状态。</summary>
public partial class PermissionRow : ObservableObject
{
    public PermissionRow(string moduleCode, string moduleName)
    {
        ModuleCode = moduleCode;
        ModuleName = moduleName;
    }

    public string ModuleCode { get; }
    public string ModuleName { get; }

    [ObservableProperty]
    private bool canCreate;

    [ObservableProperty]
    private bool canRead;

    [ObservableProperty]
    private bool canUpdate;

    [ObservableProperty]
    private bool canDelete;

    public IEnumerable<string> CheckedCodes()
    {
        if (CanCreate) yield return PermissionCatalog.Code(ModuleCode, "create");
        if (CanRead) yield return PermissionCatalog.Code(ModuleCode, "read");
        if (CanUpdate) yield return PermissionCatalog.Code(ModuleCode, "update");
        if (CanDelete) yield return PermissionCatalog.Code(ModuleCode, "delete");
    }

    public void Apply(IReadOnlySet<string> codes)
    {
        CanCreate = codes.Contains(PermissionCatalog.Code(ModuleCode, "create"));
        CanRead = codes.Contains(PermissionCatalog.Code(ModuleCode, "read"));
        CanUpdate = codes.Contains(PermissionCatalog.Code(ModuleCode, "update"));
        CanDelete = codes.Contains(PermissionCatalog.Code(ModuleCode, "delete"));
    }

    public void SetAll(bool value)
    {
        CanCreate = value;
        CanRead = value;
        CanUpdate = value;
        CanDelete = value;
    }
}

/// <summary>
/// 权限管理：固定 Admin / Designer / Viewer 三类用户，
/// 每类配置全局的增删改查权限；保存后该类用户重新登录即可获得新令牌。
/// </summary>
public partial class UserClassViewModel : ViewModelBase
{
    private readonly SessionService _session;
    private int _loadVersion;

    public ObservableCollection<RoleDto> Roles { get; } = new();
    public ObservableCollection<PermissionRow> PermissionRows { get; } = new();

    [ObservableProperty]
    private RoleDto? selectedRole;

    [ObservableProperty]
    private string? statusMessage;

    public bool IsAdminRole =>
        string.Equals(SelectedRole?.Code, "Admin", StringComparison.OrdinalIgnoreCase);

    public string HintText =>
        "权限按「模块 × 增删改查」配置，是全局权限；项目成员角色（管理/设计/只读）只决定项目准入。保存后，该类用户需要重新登录才会生效。";

    public UserClassViewModel(SessionService session)
    {
        _session = session;
        Title = "权限管理";
        _ = RefreshAsync();
    }

    public override Task RefreshAsync() => RunAsync(async () =>
    {
        var roles = await _session.Api.GetRolesAsync();
        var currentId = SelectedRole?.Id;

        Roles.Clear();
        foreach (var role in roles.OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase))
        {
            Roles.Add(role);
        }

        SelectedRole = Roles.FirstOrDefault(x => x.Id == currentId)
                       ?? Roles.FirstOrDefault(x => x.Code == "Admin")
                       ?? Roles.FirstOrDefault();
        await LoadPermissionsAsync();
    });

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (SelectedRole is null)
        {
            throw new ApiException("请选择用户类");
        }

        var codes = PermissionRows.SelectMany(x => x.CheckedCodes()).ToList();
        var saved = await _session.Api.UpdateRolePermissionsAsync(SelectedRole.Id, codes);
        StatusMessage = $"已保存「{saved.RoleCode}」的 {saved.PermissionCodes.Count} 项权限。该类用户需重新登录后生效。";
    });

    [RelayCommand]
    private void SelectAll() => SetAll(true);

    [RelayCommand]
    private void ClearAll() => SetAll(false);

    partial void OnSelectedRoleChanged(RoleDto? value)
    {
        OnPropertyChanged(nameof(IsAdminRole));
        _ = ReloadSelectedRoleAsync();
    }

    private async Task ReloadSelectedRoleAsync()
    {
        var version = ++_loadVersion;
        var role = SelectedRole;
        if (role is null)
        {
            PermissionRows.Clear();
            return;
        }

        try
        {
            var dto = await _session.Api.GetRolePermissionsAsync(role.Id);
            if (version != _loadVersion)
            {
                return;
            }

            var codes = dto.PermissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            PermissionRows.Clear();
            foreach (var module in PermissionCatalog.Modules)
            {
                var row = new PermissionRow(module.Code, module.Name);
                row.Apply(codes);
                PermissionRows.Add(row);
            }
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            if (version == _loadVersion)
            {
                ErrorMessage = "加载用户类权限失败：" + ex.Message;
            }
        }
    }

    private async Task LoadPermissionsAsync()
    {
        await ReloadSelectedRoleAsync();
    }

    private void SetAll(bool value)
    {
        foreach (var row in PermissionRows)
        {
            row.SetAll(value);
        }
    }
}
