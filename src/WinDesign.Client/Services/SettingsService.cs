using System.IO;
using System.Text.Json;

namespace WinDesign.Client.Services;

public sealed record AppSettings(string ServerUrl, string UserName);

public static class SettingsService
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WinDesign",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings is not null && !string.IsNullOrWhiteSpace(settings.ServerUrl))
                {
                    return settings;
                }
            }
        }
        catch
        {
            // ignore broken settings and use defaults
        }

        return new AppSettings("http://localhost:5080", "admin");
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings));
        }
        catch
        {
            // settings persistence is best-effort in the skeleton
        }
    }
}
