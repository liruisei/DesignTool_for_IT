namespace WinDesign.Contracts.Numbering;

/// <summary>编号规定可引用的字段名。空字符串表示留空，允许后续手动输入。</summary>
public static class NumberingFields
{
    public const string UnitCode = "UnitCode";
    public const string EquipmentTypeCode = "EquipmentTypeCode";

    public static string DisplayName(string? field) => field switch
    {
        UnitCode => "装置编号",
        EquipmentTypeCode => "设备类型编码",
        _ => "（空 / 手动输入）"
    };

    public static string Placeholder(string? field) => field switch
    {
        UnitCode => "{装置编号}",
        EquipmentTypeCode => "{设备类型编码}",
        _ => string.Empty
    };
}

/// <summary>项目级设备位号编号规定。未保存时 Id 为空，各字段为默认空值。</summary>
public sealed record NumberingRuleDto(
    Guid Id,
    Guid ProjectId,
    string PrefixField,
    string PrefixSeparator,
    int PrefixLength,
    string EquipmentTypeField,
    string TypeSeparator,
    int TypeLength,
    int SequenceLength,
    string Suffix,
    int SuffixLength);

public sealed record UpsertNumberingRuleRequest(
    string PrefixField,
    string PrefixSeparator,
    int PrefixLength,
    string EquipmentTypeField,
    string TypeSeparator,
    int TypeLength,
    int SequenceLength,
    string Suffix,
    int SuffixLength);

/// <summary>
/// 设备位号格式规则：各字段长度可配置，顺序号默认 4 位。
/// 供管理端编号规定预览与后续自动编号共用。
/// </summary>
public static class EquipmentTagFormat
{
    public const int DefaultPrefixLength = 4;
    public const int DefaultTypeLength = 5;
    public const int DefaultSequenceLength = 4;
    public const int DefaultSuffixLength = 1;
    public const int MaxLength = 64;

    public static int ClampLength(int length, int minimum = 1) =>
        Math.Clamp(length, minimum, MaxLength);

    /// <summary>按指定长度截断/右侧补空格。</summary>
    public static string Fit(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value) || length <= 0)
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > length)
        {
            trimmed = trimmed[..length];
        }
        return trimmed.PadRight(length);
    }

    public static string SequencePlaceholder(int sequenceLength) =>
        new('X', ClampLength(sequenceLength));

    /// <summary>
    /// 组合预览：字段名以占位符展示（如 {装置编号}），顺序号按长度以 X 占位。
    /// </summary>
    public static string Preview(
        string? prefixField,
        string? prefixSeparator,
        string? equipmentTypeField,
        string? typeSeparator,
        int sequenceLength,
        string? suffix,
        int suffixLength) =>
        NumberingFields.Placeholder(prefixField) +
        (prefixSeparator ?? string.Empty) +
        NumberingFields.Placeholder(equipmentTypeField) +
        (typeSeparator ?? string.Empty) +
        SequencePlaceholder(sequenceLength) +
        Fit(suffix, suffixLength);
}
