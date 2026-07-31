using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lexis.Desktop.App.Services;

/// <summary>Local desktop settings for LEXIS API (web stack on :3001).</summary>
public sealed class DeskSettings
{
    public string ApiBaseUrl { get; set; } = "http://127.0.0.1:3001";
    public string Username { get; set; } = "lexis-desktop";
    public string Password { get; set; } = "LexisDesk1!";
    public bool PreferApi { get; set; } = false;
    public bool AutoRegister { get; set; } = true;

    public static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LEXIS",
            "desktop.settings.json");

    public static DeskSettings Load()
    {
        try
        {
            var path = SettingsPath;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var s = JsonSerializer.Deserialize<DeskSettings>(json, JsonOpts);
                if (s is not null) return s;
            }
        }
        catch { /* use defaults */ }

        var defaults = new DeskSettings();
        // Env overrides (optional)
        var url = Environment.GetEnvironmentVariable("LEXIS_API_URL");
        var user = Environment.GetEnvironmentVariable("LEXIS_USER");
        var pass = Environment.GetEnvironmentVariable("LEXIS_PASS");
        if (!string.IsNullOrWhiteSpace(url)) defaults.ApiBaseUrl = url.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(user)) defaults.Username = user;
        if (!string.IsNullOrWhiteSpace(pass)) defaults.Password = pass;
        return defaults;
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOpts));
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
