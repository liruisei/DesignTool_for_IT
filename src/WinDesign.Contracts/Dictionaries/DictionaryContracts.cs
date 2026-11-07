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
    string? ExtraJson);

public sealed record CreateDictionaryItemRequest(
    string Code,
    string Name,
    int SortOrder,
    string? ExtraJson);

public sealed record UpdateDictionaryItemRequest(
    string Name,
    int SortOrder,
    bool IsActive,
    string? ExtraJson);
