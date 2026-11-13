using System.IO;
using System.Text.Json;

namespace WinDesign.Client.Services;

public sealed record AppSettings(string ServerUrl, string UserName);

/// <summary>
/// 登录界面设置。按客户端分别保存（admin / design），
/// 因此两个客户端各自记住上次登录的用户名；首次登录时用户名为空。
/// </summary>
public static class SettingsService
{
    private const string DefaultServerUrl = "http://localhost:5080";

    private static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WinDesign");

    private static string FilePath(string clientKey) =>
        Path.Combine(DirectoryPath, "settings." + clientKey + ".json");

    /// <summary>旧版本两个客户端共用的配置文件。</summary>
    private static string LegacyFilePath => Path.Combine(DirectoryPath, "settings.json");

    public static AppSettings Load(string clientKey)
    {
        var settings = TryLoad(FilePath(clientKey));
        if (settings is not null)
        {
            return settings with { UserName = settings.UserName ?? string.Empty };
        }

        // 兼容旧版共享配置：只沿用服务器地址；用户名留空，避免把管理端账号带到设计端。
        var legacy = TryLoad(LegacyFilePath);
        if (legacy is not null)
        {
            return legacy with { UserName = string.Empty };
        }

        return new AppSettings(DefaultServerUrl, string.Empty);
    }

    public static void Save(string clientKey, AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(FilePath(clientKey), JsonSerializer.Serialize(settings));
        }
        catch
        {
            // settings persistence is best-effort in the skeleton
        }
    }

    private static AppSettings? TryLoad(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            return settings is not null && !string.IsNullOrWhiteSpace(settings.ServerUrl)
                ? settings
                : null;
        }
        catch
        {
            return null;
        }
    }
}
