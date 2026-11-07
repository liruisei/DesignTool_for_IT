namespace WinDesign.Contracts.Projects;

public sealed record ProjectDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ClientName { get; init; }
    public string? Location { get; init; }
    public string? Phase { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? OwnerUserId { get; init; }
    public string? OwnerName { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record CreateProjectRequest
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ClientName { get; init; }
    public string? Location { get; init; }
    public string? Phase { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? Description { get; init; }
}

public sealed record UpdateProjectRequest
{
    public string Name { get; init; } = string.Empty;
    public string? ClientName { get; init; }
    public string? Location { get; init; }
    public string? Phase { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? Status { get; init; }
    public string? Description { get; init; }
}
