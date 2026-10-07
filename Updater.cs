using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace MxBattery;

public sealed record Release(Version Version, string ZipUrl, string ShaUrl);

/// <summary>
/// Self-update from GitHub Releases. A release tagged vX.Y.Z carries MxBattery-X.Y.Z.zip and MxBattery-X.Y.Z.zip.sha256.
/// The running exe is renamed to .old (Windows allows that), the new one takes its place, and a new instance
/// waits for this process to exit before taking the single-instance mutex.
/// </summary>
public static class Updater
{
    // MXBATTERY_UPDATE_API overrides the endpoint for local testing only.
    static string Api => Environment.GetEnvironmentVariable("MXBATTERY_UPDATE_API")
        ?? "https://api.github.com/repos/Krab1/mx3s-tools/releases/latest";

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5), DefaultRequestHeaders = { { "User-Agent", "MxBattery-updater" } } };

    public static Version Current
    {
        get { var v = typeof(Updater).Assembly.GetName().Version!; return new(v.Major, v.Minor, Math.Max(v.Build, 0)); }
    }

    /// <summary>
    /// Release newer than the running one (null = none). Reachable is false when the server can't be used at all:
    /// offline, repo made private or deleted (404), rate limited, or an unexpected response. Never throws,
    /// so closing the repo later just turns updating off quietly.
    /// </summary>
    public static async Task<(Release? Release, bool Reachable)> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(await Http.GetStringAsync(Api, ct));
            var root = doc.RootElement;
            if (!Version.TryParse(root.GetProperty("tag_name").GetString()?.TrimStart('v', 'V'), out var v) || v <= Current) return (null, true);
            string? zip = null, sha = null;
            foreach (var a in root.GetProperty("assets").EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                var url = a.GetProperty("browser_download_url").GetString();
                if (name.EndsWith(".zip.sha256", StringComparison.OrdinalIgnoreCase)) sha = url;
                else if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) zip = url;
            }
            return (zip is null || sha is null ? null : new Release(v, zip, sha), true);   // no checksum = not installable
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return (null, false); }                                                      // any other failure: treat as unreachable
    }

    /// <summary>Download, verify, swap the exe and start the new instance. Throws on any failure, leaving the old exe in place. Caller must exit afterwards.</summary>
    public static async Task ApplyAsync(Release r, CancellationToken ct = default)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("no process path");
        var zip = await Http.GetByteArrayAsync(r.ZipUrl, ct);
        var expected = (await Http.GetStringAsync(r.ShaUrl, ct)).Trim().Split([' ', '\t', '*'])[0];
        if (!Convert.ToHexString(SHA256.HashData(zip)).Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("update checksum mismatch");

        using var archive = new ZipArchive(new MemoryStream(zip));
        var entry = archive.Entries.FirstOrDefault(e => e.Name.Equals("MxBattery.exe", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("MxBattery.exe not in update package");
        var next = exe + ".new";
        entry.ExtractToFile(next, true);

        File.Move(exe, exe + ".old", true);
        try { File.Move(next, exe, true); }
        catch { File.Move(exe + ".old", exe, true); throw; }                     // roll back
        Process.Start(new ProcessStartInfo(exe, $"--wait-pid {Environment.ProcessId}") { UseShellExecute = false });
    }

    /// <summary>Remove the previous version left behind by an update.</summary>
    public static void CleanUp()
    {
        try { File.Delete(Environment.ProcessPath + ".old"); } catch { }
    }
}
