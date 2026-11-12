using System.Text.Json;
using System.Text.Json.Nodes;

namespace WinDesign.Contracts.Dictionaries;

/// <summary>
/// 编码项 <c>ExtraJson</c> 的读写辅助。
/// 设备类型（EQUIPMENT_TYPE）的「所属专业」保存在这里：开发库使用 EnsureCreated，
/// 不重建数据库即可增加该字段；API 对外仍以强类型 <c>Discipline</c> 暴露。
/// </summary>
public static class DictionaryItemExtras
{
    private const string DisciplineKey = "discipline";

    /// <summary>读取设备所属专业；未配置或格式异常时返回 null。</summary>
    public static string? GetDiscipline(string? extraJson)
    {
        if (string.IsNullOrWhiteSpace(extraJson))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(extraJson) is JsonObject obj &&
                   obj[DisciplineKey] is JsonValue value &&
                   value.TryGetValue<string>(out var discipline)
                ? discipline
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>写入或清除设备所属专业，保留 ExtraJson 中的其他键。</summary>
    public static string? SetDiscipline(string? extraJson, string? discipline)
    {
        JsonObject obj;
        try
        {
            obj = string.IsNullOrWhiteSpace(extraJson)
                ? new JsonObject()
                : JsonNode.Parse(extraJson) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            obj = new JsonObject();
        }

        if (string.IsNullOrWhiteSpace(discipline))
        {
            obj.Remove(DisciplineKey);
        }
        else
        {
            obj[DisciplineKey] = discipline.Trim();
        }

        return obj.Count == 0 ? null : obj.ToJsonString();
    }
}
