namespace WinDesign.Contracts.Dictionaries;

public sealed record DictionaryTypeDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsSystem,
    int SortOrder,
    int ItemCount);

public sealed record CreateDictionaryTypeRequest(
    string Code,
    string Name,
    string? Description,
    int SortOrder);

public sealed record UpdateDictionaryTypeRequest(
    string Name,
    string? Description,
    int SortOrder);

public sealed record DictionaryItemDto(
    Guid Id,
    Guid TypeId,
    string TypeCode,
    string Code,
    string Name,
    int SortOrder,
    bool IsActive,
    string? ExtraJson)
{
    /// <summary>设备类型（EQUIPMENT_TYPE）的所属专业；其他编码类型为 null。</summary>
    public string? Discipline { get; init; }
}

public sealed record CreateDictionaryItemRequest(
    string Code,
    string Name,
    int SortOrder,
    string? ExtraJson)
{
    /// <summary>设备类型（EQUIPMENT_TYPE）的所属专业，如 Instrument / Telecom / IT。</summary>
    public string? Discipline { get; init; }
}

public sealed record UpdateDictionaryItemRequest(
    string Name,
    int SortOrder,
    bool IsActive,
    string? ExtraJson)
{
    /// <summary>设备类型（EQUIPMENT_TYPE）的所属专业，如 Instrument / Telecom / IT。</summary>
    public string? Discipline { get; init; }
}
