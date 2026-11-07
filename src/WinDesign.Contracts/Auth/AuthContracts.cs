namespace WinDesign.Contracts.Auth;

public sealed record LoginRequest(string UserName, string Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAt,
    CurrentUserDto User);

public sealed record CurrentUserDto(
    Guid Id,
    string UserName,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
