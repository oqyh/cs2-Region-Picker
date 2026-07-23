using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace CS2RegionPicker;

public static class Updater
{
    public const string Repo = "oqyh/cs2-Region-Picker";
    const string ApiLatest = "https://api.github.com/repos/" + Repo + "/releases/latest";
    public const string ReleasesPage = "https://github.com/" + Repo + "/releases/latest";

    static readonly HttpClient Http = CreateClient(TimeSpan.FromMinutes(5), true);
    static readonly HttpClient NoRedirect = CreateClient(TimeSpan.FromSeconds(15), false);

    static HttpClient CreateClient(TimeSpan timeout, bool followRedirects)
    {
        var c = new HttpClient(new System.Net.Http.HttpClientHandler { AllowAutoRedirect = followRedirects })
        {
            Timeout = timeout
        };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("CS2RegionPicker");
        return c;
    }

    public static Version Current
    {
        get
        {
            Version v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
        }
    }

    public sealed record ReleaseInfo(Version Version, string Tag, string? ExeUrl);

    public static async Task<ReleaseInfo?> CheckAsync()
    {
        ReleaseInfo? info = await CheckViaRedirectAsync();
        if (info != null) return info;
        return await CheckViaApiAsync();
    }

    static async Task<ReleaseInfo?> CheckViaRedirectAsync()
    {
        try
        {
            using HttpResponseMessage resp = await NoRedirect.GetAsync(
                "https://github.com/" + Repo + "/releases/latest");

            Uri? loc = resp.Headers.Location;
            if (loc == null) return null;

            string path = loc.ToString();
            int i = path.LastIndexOf("/tag/", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;

            string tag = Uri.UnescapeDataString(path.Substring(i + 5).Trim('/'));
            Version? v = ParseTag(tag);
            if (v == null) return null;

            string exeUrl = "https://github.com/" + Repo + "/releases/download/" +
                            Uri.EscapeDataString(tag) + "/CS2RegionPicker.exe";
            return new ReleaseInfo(v, tag, exeUrl);
        }
        catch { return null; }
    }

    static Version? ParseTag(string tag)
    {
        string clean = tag.TrimStart('v', 'V').Trim();
        if (clean.Length == 0) return null;
        if (!clean.Contains('.')) clean += ".0";
        if (!Version.TryParse(clean, out Version? v)) return null;
        return new Version(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0));
    }

    static async Task<ReleaseInfo?> CheckViaApiAsync()
    {
        string json = await Http.GetStringAsync(ApiLatest);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string tag = root.TryGetProperty("tag_name", out var t) && t.ValueKind == JsonValueKind.String
            ? t.GetString() ?? "" : "";
        Version? latest = ParseTag(tag);
        if (latest == null) return null;

        string? exeUrl = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in assets.EnumerateArray())
            {
                string name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                    a.TryGetProperty("browser_download_url", out var u))
                {
                    exeUrl = u.GetString();
                    break;
                }
            }
        }

        return new ReleaseInfo(latest, tag, exeUrl);
    }

    public static async Task DownloadAndInstallAsync(string exeUrl, IProgress<int>? progress, Action? onInstalling = null)
    {
        string currentExe = Environment.ProcessPath ?? throw new Exception("Cannot resolve exe path.");
        string temp = Path.Combine(Path.GetTempPath(), "CS2RegionPicker_new_" + Guid.NewGuid().ToString("N") + ".exe");

        using (HttpResponseMessage resp = await Http.GetAsync(exeUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? -1;
            await using Stream src = await resp.Content.ReadAsStreamAsync();
            await using FileStream dst = File.Create(temp);
            byte[] buf = new byte[81920];
            long done = 0;
            int lastPct = -1;
            int read;
            while ((read = await src.ReadAsync(buf)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, read));
                done += read;
                if (total > 0)
                {
                    int pct = (int)(done * 100 / total);
                    if (pct != lastPct)
                    {
                        lastPct = pct;
                        progress?.Report(pct);
                    }
                }
            }
        }

        if (new FileInfo(temp).Length < 200_000)
        {
            try { File.Delete(temp); } catch { }
            throw new Exception("Downloaded file looks invalid.");
        }

        onInstalling?.Invoke();

        string old = currentExe + ".old";
        try { if (File.Exists(old)) File.Delete(old); } catch { }

        File.Move(currentExe, old);
        try
        {
            File.Move(temp, currentExe);
        }
        catch
        {
            File.Move(old, currentExe);
            throw;
        }

        Process.Start(new ProcessStartInfo(currentExe) { UseShellExecute = true });
        Application.Current.Shutdown();
    }

    public static void CleanupOldVersion()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            string? dir = exe == null ? null : Path.GetDirectoryName(exe);
            if (dir == null) return;

            foreach (string f in Directory.GetFiles(dir, "CS2RegionPicker*.old"))
            {
                try { File.Delete(f); } catch { }
            }
        }
        catch { }
    }

    public static void OpenReleasesPage()
    {
        try { Process.Start(new ProcessStartInfo(ReleasesPage) { UseShellExecute = true }); }
        catch { }
    }
}
