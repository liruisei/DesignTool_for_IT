namespace WinDesign.Contracts.Projects;

public sealed record ProjectDto(
    Guid Id,
    string Code,
    string Name,
    string? ClientName,
    string? Phase,
    string Status,
    string? Description,
    Guid? OwnerUserId,
    string? OwnerName,
    DateTime CreatedAt);

public sealed record CreateProjectRequest(
    string Code,
    string Name,
    string? ClientName,
    string? Phase,
    string? Description);

public sealed record UpdateProjectRequest(
    string Name,
    string? ClientName,
    string? Phase,
    string? Status,
    string? Description);
