namespace WinDesign.Contracts.Users;

public sealed record UserDto(
    Guid Id,
    string UserName,
    string DisplayName,
    string? Email,
    string? Phone,
    bool IsActive,
    bool IsSystemAdmin,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public sealed record CreateUserRequest(
    string UserName,
    string DisplayName,
    string Password,
    string? Email,
    string? Phone,
    IReadOnlyList<string> Roles);

public sealed record UpdateUserRequest(
    string DisplayName,
    string? Email,
    string? Phone,
    bool IsActive,
    IReadOnlyList<string> Roles);

public sealed record ResetPasswordRequest(string NewPassword);

public sealed record RoleDto(Guid Id, string Code, string Name, string? Description);
