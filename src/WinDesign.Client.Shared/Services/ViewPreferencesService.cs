using System.IO;
using System.Linq;
using System.Text.Json;

namespace WinDesign.Client.Services;

/// <summary>设计端表格的本地界面偏好；按登录账号分别保存。</summary>
public sealed class ViewPreferences
{
    /// <summary>索引表冻结列数（勾选列之外的数据列）；默认 0。</summary>
    public int IndexTableFrozenColumns { get; set; }

    /// <summary>信息设计-房间明细冻结列数（勾选列之外的数据列）；默认 0。</summary>
    public int RoomTableFrozenColumns { get; set; }

    /// <summary>索引表列顺序（按表头文字保存）；为空表示使用默认顺序。</summary>
    public List<string>? IndexTableColumnOrder { get; set; }

    /// <summary>房间明细列顺序（按表头文字保存）；为空表示使用默认顺序。</summary>
    public List<string>? RoomTableColumnOrder { get; set; }
}

/// <summary>
/// 冻结列与列顺序的本地持久化：按登录账号分文件保存，改动即保存。
/// 账号 A 调整后，账号 B 登录仍看到自己的设置或默认值。
/// </summary>
public static class ViewPreferencesService
{
    private static readonly object Sync = new();
    private static ViewPreferences? _current;
    private static string _userKey = "default";

    private static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WinDesign");

    private static string FilePath => Path.Combine(
        DirectoryPath,
        "preferences.design." + _userKey + ".json");

    /// <summary>登录或切换账号后调用；不同账号使用各自的偏好文件。</summary>
    public static void SetUser(string? userName)
    {
        lock (Sync)
        {
            var key = SanitizeUserKey(userName);
            if (string.Equals(_userKey, key, StringComparison.Ordinal) && _current is not null)
            {
                return;
            }

            _userKey = key;
            _current = null;
        }
    }

    public static int IndexTableFrozenColumns
    {
        get
        {
            lock (Sync)
            {
                return GetCurrent().IndexTableFrozenColumns;
            }
        }
        set
        {
            lock (Sync)
            {
                var current = GetCurrent();
                var clamped = Math.Max(0, value);
                if (current.IndexTableFrozenColumns == clamped)
                {
                    return;
                }

                current.IndexTableFrozenColumns = clamped;
                SaveCore(current);
            }
        }
    }

    public static IReadOnlyList<string>? IndexTableColumnOrder
    {
        get
        {
            lock (Sync)
            {
                return GetCurrent().IndexTableColumnOrder?.ToArray();
            }
        }
        set
        {
            lock (Sync)
            {
                var current = GetCurrent();
                var next = value?.ToList();
                if (OrdersEqual(current.IndexTableColumnOrder, next))
                {
                    return;
                }

                current.IndexTableColumnOrder = next;
                SaveCore(current);
            }
        }
    }

    public static int RoomTableFrozenColumns
    {
        get
        {
            lock (Sync)
            {
                return GetCurrent().RoomTableFrozenColumns;
            }
        }
        set
        {
            lock (Sync)
            {
                var current = GetCurrent();
                var clamped = Math.Max(0, value);
                if (current.RoomTableFrozenColumns == clamped)
                {
                    return;
                }

                current.RoomTableFrozenColumns = clamped;
                SaveCore(current);
            }
        }
    }

    public static IReadOnlyList<string>? RoomTableColumnOrder
    {
        get
        {
            lock (Sync)
            {
                return GetCurrent().RoomTableColumnOrder?.ToArray();
            }
        }
        set
        {
            lock (Sync)
            {
                var current = GetCurrent();
                var next = value?.ToList();
                if (OrdersEqual(current.RoomTableColumnOrder, next))
                {
                    return;
                }

                current.RoomTableColumnOrder = next;
                SaveCore(current);
            }
        }
    }

    private static ViewPreferences GetCurrent() => _current ??= LoadCore();

    private static ViewPreferences LoadCore()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                if (JsonSerializer.Deserialize<ViewPreferences>(json) is { } preferences)
                {
                    return preferences;
                }
            }
        }
        catch
        {
            // 界面偏好读取失败时回退默认值，不影响业务。
        }

        return new ViewPreferences();
    }

    private static void SaveCore(ViewPreferences preferences)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(preferences));
        }
        catch
        {
            // 界面偏好保存是尽力而为，失败不打断用户操作。
        }
    }

    private static string SanitizeUserKey(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return "default";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var chars = userName.Trim().ToLowerInvariant()
            .Select(c => invalid.Contains(c) ? '_' : c)
            .ToArray();
        return new string(chars);
    }

    private static bool OrdersEqual(IReadOnlyList<string>? left, IReadOnlyList<string>? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left.SequenceEqual(right, StringComparer.Ordinal);
    }
}
