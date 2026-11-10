using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace WinDesign.Contracts.Equipment;

/// <summary>
/// 规格书参数与 ContentJson 之间的唯一转换点。
/// 读取容错（坏 JSON 不抛异常，返回空参数 + 警告），写入前统一规范化。
/// </summary>
public static class SpecContentCodec
{
    public const int MaxParameters = 200;
    public const int MaxKeyLength = 128;
    public const int MaxValueLength = 4000;

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        // 中文参数名不转义为 \uXXXX，便于人工核对与导出。
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 从存储的 ContentJson 解析参数。任何格式问题都降级为空参数并给出警告，绝不抛异常。
    /// 非字符串值转换为原始 JSON 文本；重复参数名保留第一条。
    /// </summary>
    public static IReadOnlyList<SpecParameterDto> Parse(string? contentJson, out string? warning)
    {
        warning = null;
        if (string.IsNullOrWhiteSpace(contentJson))
        {
            return Array.Empty<SpecParameterDto>();
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(contentJson);
        }
        catch (JsonException)
        {
            warning = "原有规格内容不是有效的 JSON 对象，已按空参数处理";
            return Array.Empty<SpecParameterDto>();
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                warning = "原有规格内容不是 JSON 对象，已按空参数处理";
                return Array.Empty<SpecParameterDto>();
            }

            var parameters = new List<SpecParameterDto>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var hasDuplicate = false;
            var hasNonStringValue = false;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!seenKeys.Add(property.Name))
                {
                    hasDuplicate = true;
                    continue;
                }

                string value;
                switch (property.Value.ValueKind)
                {
                    case JsonValueKind.String:
                        value = property.Value.GetString() ?? string.Empty;
                        break;
                    case JsonValueKind.Null:
                        value = string.Empty;
                        break;
                    default:
                        value = property.Value.GetRawText();
                        hasNonStringValue = true;
                        break;
                }

                parameters.Add(new SpecParameterDto { Key = property.Name, Value = value });
            }

            if (hasDuplicate)
            {
                warning = "存在重复参数名，已保留第一条";
            }
            else if (hasNonStringValue)
            {
                warning = "部分参数值不是字符串，保存后将转换为文本";
            }

            return parameters;
        }
    }

    /// <summary>
    /// 校验并规范化客户端提交的参数行：去掉空行、修剪首尾空白、拒绝重复参数名与超长内容。
    /// </summary>
    public static bool TryNormalize(
        IEnumerable<SpecParameterDto>? parameters,
        out IReadOnlyList<SpecParameterDto> normalized,
        out string? error)
    {
        normalized = Array.Empty<SpecParameterDto>();
        error = null;
        if (parameters is null)
        {
            return true;
        }

        var result = new List<SpecParameterDto>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in parameters)
        {
            // JSON 数组允许 null 元素；反序列化后 row 可能为 null，必须转成 400 而不是 500。
            if (row is null)
            {
                error = "参数行无效";
                return false;
            }

            var key = (row.Key ?? string.Empty).Trim();
            var value = (row.Value ?? string.Empty).Trim();

            // 整行为空的编辑残留直接丢弃；允许有参数名但暂时没有值。
            if (key.Length == 0 && value.Length == 0)
            {
                continue;
            }
            if (key.Length == 0)
            {
                error = "参数名不能为空";
                return false;
            }
            if (key.Length > MaxKeyLength)
            {
                error = $"参数名不能超过 {MaxKeyLength} 个字符";
                return false;
            }
            if (value.Length > MaxValueLength)
            {
                error = $"参数值不能超过 {MaxValueLength} 个字符";
                return false;
            }
            if (!seenKeys.Add(key))
            {
                error = $"参数名重复：{key}";
                return false;
            }

            result.Add(new SpecParameterDto { Key = key, Value = value });
            if (result.Count > MaxParameters)
            {
                error = $"参数数量不能超过 {MaxParameters} 个";
                return false;
            }
        }

        normalized = result;
        return true;
    }

    /// <summary>按输入顺序序列化为 JSON 对象，值与键均按字符串写入。</summary>
    public static string Serialize(IEnumerable<SpecParameterDto> parameters)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            foreach (var parameter in parameters)
            {
                writer.WriteString(parameter.Key, parameter.Value);
            }
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
