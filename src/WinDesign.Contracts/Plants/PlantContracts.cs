namespace WinDesign.Contracts.Plants;

public sealed record PlantDto(
    Guid Id,
    string Code,
    string Name,
    string? ClientName,
    string? Location,
    string? Description,
    bool IsActive,
    int ProjectCount,
    int NodeCount,
    DateTime CreatedAt);

public sealed record CreatePlantRequest(
    string Code,
    string Name,
    string? ClientName,
    string? Location,
    string? Description);

public sealed record UpdatePlantRequest(
    string Name,
    string? ClientName,
    string? Location,
    string? Description,
    bool IsActive);
