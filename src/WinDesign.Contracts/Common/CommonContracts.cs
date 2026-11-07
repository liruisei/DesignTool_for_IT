namespace WinDesign.Contracts.Common;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record ApiError(string Message, string? Details = null);

public sealed record DashboardDto(
    int UserCount,
    int ProjectCount,
    int DictionaryTypeCount,
    int EquipmentCount,
    IReadOnlyList<Projects.ProjectDto> RecentProjects);
