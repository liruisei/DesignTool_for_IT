using WinDesign.Client.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinDesign.Client.Services;
using WinDesign.Contracts.Users;

namespace WinDesign.AdminClient.ViewModels;

public partial class RoleOption : ObservableObject
{
    public RoleOption(string code, string name)
    {
        Code = code;
        Name = name;
    }

    public string Code { get; }
    public string Name { get; }

    [ObservableProperty]
    private bool isSelected;
}

/// <summary>
/// 用户列表行。UserDto 是不可变记录，这里包一层可修改的启用状态，
/// 列表中的勾选先留在本地，点击「保存」后统一提交。
/// </summary>
public partial class UserRowItem : ObservableObject
{
    public UserRowItem(UserDto user)
    {
        User = user;
        isActive = user.IsActive;
    }

    public UserDto User { get; }
    public Guid Id => User.Id;
    public string UserName => User.UserName;
    public string DisplayName => User.DisplayName;
    public string RolesText => User.RolesText;
    public string? Email => User.Email;
    public DateTime? LastLoginAt => User.LastLoginAt;

    [ObservableProperty]
    private bool isActive;

    /// <summary>与服务端保存值不同即表示有未保存的启用状态修改。</summary>
    public bool IsActiveDirty => IsActive != User.IsActive;

    partial void OnIsActiveChanged(bool value) => OnPropertyChanged(nameof(IsActiveDirty));
}

public partial class UserManagementViewModel : ViewModelBase, IPendingChanges
{
    private readonly SessionService _session;

    public ObservableCollection<UserDto> Users { get; } = new();
    public ObservableCollection<UserRowItem> UserRows { get; } = new();
    public ObservableCollection<RoleOption> RoleOptions { get; } = new();

    [ObservableProperty]
    private UserDto? selectedUser;

    [ObservableProperty]
    private UserRowItem? selectedUserRow;

    /// <summary>列表中是否有未保存的启用/停用修改。</summary>
    public bool HasPendingActiveChanges => UserRows.Any(x => x.IsActiveDirty);

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string editorTitle = "新建用户";

    [ObservableProperty]
    private bool isCreating = true;

    [ObservableProperty]
    private string editUserName = string.Empty;

    [ObservableProperty]
    private string editDisplayName = string.Empty;

    [ObservableProperty]
    private string editEmail = string.Empty;

    [ObservableProperty]
    private string editPhone = string.Empty;

    [ObservableProperty]
    private string editPassword = string.Empty;

    [ObservableProperty]
    private bool editIsActive = true;

    [ObservableProperty]
    private string? statusMessage;

    public UserManagementViewModel(SessionService session)
    {
        _session = session;
        Title = "用户管理";
        _ = RefreshAsync();
    }

    public override Task RefreshAsync() => RunAsync(async () =>
    {
        var roles = await _session.Api.GetRolesAsync();
        RoleOptions.Clear();
        foreach (var role in roles)
        {
            RoleOptions.Add(new RoleOption(role.Code, role.Name));
        }
        await LoadUsersAsync();
    });

    [RelayCommand]
    private Task SearchAsync() => RunAsync(LoadUsersAsync);

    /// <summary>编辑已有用户时自动保存；新建未保存的用户不自动创建。</summary>
    public async Task SavePendingAsync()
    {
        if (IsCreating || SelectedUser is null)
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
    private void NewUser()
    {
        SelectedUser = null;
        IsCreating = true;
        EditorTitle = "新建用户";
        EditUserName = string.Empty;
        EditDisplayName = string.Empty;
        EditEmail = string.Empty;
        EditPhone = string.Empty;
        EditPassword = string.Empty;
        EditIsActive = true;
        StatusMessage = null;
        foreach (var option in RoleOptions)
        {
            option.IsSelected = option.Code == "Viewer";
        }
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        // 记录保存开始时正在编辑的用户，保存完成后若用户已切换选择则不要抢回。
        var editedUserId = SelectedUser?.Id;
        var roles = RoleOptions.Where(x => x.IsSelected).Select(x => x.Code).ToList();
        if (roles.Count == 0)
        {
            roles.Add("Viewer");
        }

        UserDto saved;
        if (IsCreating)
        {
            if (string.IsNullOrWhiteSpace(EditUserName) || string.IsNullOrWhiteSpace(EditDisplayName))
            {
                throw new ApiException("用户名和显示名称不能为空");
            }
            if (string.IsNullOrWhiteSpace(EditPassword) || EditPassword.Length < 6)
            {
                throw new ApiException("密码至少 6 位");
            }

            saved = await _session.Api.CreateUserAsync(new CreateUserRequest(
                EditUserName.Trim(),
                EditDisplayName.Trim(),
                EditPassword,
                NullIfEmpty(EditEmail),
                NullIfEmpty(EditPhone),
                roles));
            StatusMessage = "用户已创建";
        }
        else
        {
            if (SelectedUser is null)
            {
                throw new ApiException("请选择用户");
            }

            saved = await _session.Api.UpdateUserAsync(SelectedUser.Id, new UpdateUserRequest(
                EditDisplayName.Trim(),
                NullIfEmpty(EditEmail),
                NullIfEmpty(EditPhone),
                EditIsActive,
                roles));
            StatusMessage = "用户已保存";
        }

        EditPassword = string.Empty;
        await LoadUsersAsync();
        if (SelectedUser is null || SelectedUser.Id == editedUserId)
        {
            SelectedUser = Users.FirstOrDefault(x => x.Id == saved.Id);
        }
    });

    [RelayCommand]
    private Task ResetPasswordAsync() => RunAsync(async () =>
    {
        if (SelectedUser is null)
        {
            throw new ApiException("请选择用户");
        }
        if (string.IsNullOrWhiteSpace(EditPassword) || EditPassword.Length < 6)
        {
            throw new ApiException("请输入至少 6 位的新密码");
        }

        await _session.Api.ResetPasswordAsync(SelectedUser.Id, EditPassword);
        EditPassword = string.Empty;
        StatusMessage = "密码已重置";
        await LoadUsersAsync();
    });

    [RelayCommand]
    private Task ToggleActiveAsync() => RunAsync(async () =>
    {
        if (SelectedUser is null)
        {
            throw new ApiException("请选择用户");
        }

        var updated = await _session.Api.ToggleUserActiveAsync(SelectedUser.Id);
        await LoadUsersAsync();
        SelectedUser = Users.FirstOrDefault(x => x.Id == updated.Id);
        StatusMessage = updated.IsActive ? "用户已启用" : "用户已停用";
    });

    /// <summary>批量保存列表中勾选的启用/停用状态（新建按钮右侧的「保存」）。</summary>
    [RelayCommand]
    private Task SaveActiveChanges() => RunAsync(async () =>
    {
        var dirty = UserRows.Where(x => x.IsActiveDirty).ToList();
        if (dirty.Count == 0)
        {
            StatusMessage = "没有需要保存的启用状态";
            return;
        }

        if (_session.User?.Id is Guid selfId &&
            dirty.Any(x => x.Id == selfId && !x.IsActive))
        {
            throw new ApiException("不能停用当前登录用户");
        }

        try
        {
            foreach (var row in dirty)
            {
                await _session.Api.ToggleUserActiveAsync(row.Id);
            }

            StatusMessage = $"已保存 {dirty.Count} 个用户的启用状态";
        }
        finally
        {
            // 无论成功与否都刷新，让列表与服务器状态一致。
            await LoadUsersAsync();
        }
    });

    [RelayCommand]
    private Task DeleteUser() => RunAsync(async () =>
    {
        if (SelectedUser is null)
        {
            throw new ApiException("请选择用户");
        }

        var deleted = SelectedUser;
        await _session.Api.DeleteUserAsync(deleted.Id);

        NewUser();
        await LoadUsersAsync();
        StatusMessage = $"已删除用户：{deleted.UserName}";
    });

    partial void OnSelectedUserChanged(UserDto? value)
    {
        if (value is null)
        {
            if (SelectedUserRow is not null)
            {
                SelectedUserRow = null;
            }
            return;
        }

        var row = UserRows.FirstOrDefault(x => x.Id == value.Id);
        if (row is not null && !ReferenceEquals(SelectedUserRow, row))
        {
            SelectedUserRow = row;
        }

        IsCreating = false;
        EditorTitle = "编辑用户：" + value.UserName;
        EditUserName = value.UserName;
        EditDisplayName = value.DisplayName;
        EditEmail = value.Email ?? string.Empty;
        EditPhone = value.Phone ?? string.Empty;
        EditIsActive = value.IsActive;
        EditPassword = string.Empty;
        StatusMessage = null;
        foreach (var option in RoleOptions)
        {
            option.IsSelected = value.Roles.Contains(option.Code, StringComparer.OrdinalIgnoreCase);
        }
    }

    partial void OnSelectedUserRowChanged(UserRowItem? value)
    {
        if (value is not null && SelectedUser?.Id != value.Id)
        {
            SelectedUser = value.User;
        }
    }

    private void OnUserRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UserRowItem.IsActive))
        {
            OnPropertyChanged(nameof(HasPendingActiveChanges));
        }
    }

    private async Task LoadUsersAsync()
    {
        var result = await _session.Api.GetUsersAsync(SearchText);
        var currentId = SelectedUser?.Id;

        foreach (var row in UserRows)
        {
            row.PropertyChanged -= OnUserRowPropertyChanged;
        }
        UserRows.Clear();
        Users.Clear();

        foreach (var user in result.Items)
        {
            Users.Add(user);
            var row = new UserRowItem(user);
            row.PropertyChanged += OnUserRowPropertyChanged;
            UserRows.Add(row);
        }

        if (currentId is not null)
        {
            SelectedUser = Users.FirstOrDefault(x => x.Id == currentId);
            SelectedUserRow = UserRows.FirstOrDefault(x => x.Id == currentId);
        }
        else
        {
            SelectedUserRow = null;
        }

        OnPropertyChanged(nameof(HasPendingActiveChanges));
    }

    public override void Dispose()
    {
        foreach (var row in UserRows)
        {
            row.PropertyChanged -= OnUserRowPropertyChanged;
        }

        base.Dispose();
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
