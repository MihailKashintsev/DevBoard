using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DevBoard.Services;

public static class UpdateService
{
    private const string RepoOwner = "MihailKashintsev";
    private const string RepoName = "DevBoard";
    private const string ApiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";

    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "DevBoard-Updater" }
        }
    };

    public static Version GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var attr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (attr != null && Version.TryParse(attr.InformationalVersion.Split('+')[0], out var v))
            return v;
        return assembly.GetName().Version ?? new Version(1, 0, 0);
    }

    public static async Task<UpdateInfo?> CheckForUpdateAsync()
    {
        try
        {
            var response = await Http.GetAsync(ApiUrl);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);
            if (release == null || string.IsNullOrEmpty(release.TagName)) return null;

            var tagVersion = release.TagName.Replace("v", "").Trim();
            if (!Version.TryParse(tagVersion, out var remoteVersion)) return null;

            var currentVersion = GetCurrentVersion();
            if (remoteVersion <= currentVersion) return null;

            var installerAsset = release.Assets?.Find(a =>
                a.Name != null &&
                a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                a.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase));

            return new UpdateInfo
            {
                Version = remoteVersion,
                TagName = release.TagName,
                ReleaseNotes = release.Body ?? "",
                DownloadUrl = installerAsset?.BrowserDownloadUrl ?? ""
            };
        }
        catch
        {
            return null;
        }
    }

    public static async Task<bool> DownloadAndInstallAsync(UpdateInfo update, Action<string>? onProgress = null)
    {
        if (string.IsNullOrEmpty(update.DownloadUrl)) return false;

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "DevBoard_Update");
            Directory.CreateDirectory(tempDir);

            var installerPath = Path.Combine(tempDir, $"DevBoard-Setup-{update.Version}.exe");

            onProgress?.Invoke("Скачивание обновления...");
            var response = await Http.GetAsync(update.DownloadUrl);
            response.EnsureSuccessStatusCode();

            await using var fs = new FileStream(installerPath, FileMode.Create);
            await response.Content.CopyToAsync(fs);
            fs.Close();

            onProgress?.Invoke("Запуск установщика...");

            var process = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/SILENT /NORESTART",
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(process);

            Environment.Exit(0);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public class UpdateInfo
{
    public Version Version { get; set; } = new(1, 0, 0);
    public string TagName { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
}

public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("assets")]
    public System.Collections.Generic.List<GitHubAsset>? Assets { get; set; }
}

public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }
}
