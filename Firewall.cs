using System.Diagnostics;
using System.IO;
using System.Text;

namespace CS2RegionPicker;

public static class Firewall
{
    public const string RulePrefix = "CS2RegionPicker_";

    public static async Task<HashSet<string>> GetBlockedPopsAsync()
    {
        string script =
            "Get-NetFirewallRule -DisplayName '" + RulePrefix + "*' -ErrorAction SilentlyContinue | " +
            "ForEach-Object { $_.DisplayName }";

        (int exitCode, string output) = await RunPowerShellAsync(script);

        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in output.Split('\n'))
        {
            string name = line.Trim();
            if (!name.StartsWith(RulePrefix, StringComparison.OrdinalIgnoreCase)) continue;

            if (name.Equals(RulePrefix + "selftest", StringComparison.OrdinalIgnoreCase)) continue;

            string code = name.Substring(RulePrefix.Length);

            foreach (string suffix in new[] { "_udp_out", "_udp_in", "_tcp_out", "_tcp_in", "_out", "_in" })
            {
                if (code.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    code = code.Substring(0, code.Length - suffix.Length);
                    break;
                }
            }

            if (code.Length > 0) blocked.Add(code);
        }
        return blocked;
    }

    public static async Task ApplyAsync(List<(string Code, List<string> Ips)> blockedPops)
    {
        var sb = new StringBuilder();
        sb.AppendLine("$ErrorActionPreference = 'Stop'");
        sb.AppendLine("Get-NetFirewallRule -DisplayName '" + RulePrefix + "*' -ErrorAction SilentlyContinue | Remove-NetFirewallRule");

        foreach ((string code, List<string> ips) in blockedPops)
        {
            if (ips.Count == 0)
            {
                continue;
            }

            string safeCode = Sanitize(code);
            string ipList = string.Join(",", ips.Select(ip => "'" + Sanitize(ip) + "'"));
            string desc = "'CS2 Region Picker (GoldKingZ) - blocks Valve SDR relays for pop " + safeCode + "'";

            foreach (string proto in new[] { "UDP", "TCP" })
            {
                sb.AppendLine(
                    "New-NetFirewallRule -DisplayName '" + RulePrefix + safeCode + "_" + proto.ToLowerInvariant() + "_out'" +
                    " -Description " + desc +
                    " -Direction Outbound -Action Block -Enabled True -Profile Any" +
                    " -Protocol " + proto +
                    " -RemoteAddress " + ipList + " | Out-Null");
                sb.AppendLine(
                    "New-NetFirewallRule -DisplayName '" + RulePrefix + safeCode + "_" + proto.ToLowerInvariant() + "_in'" +
                    " -Description " + desc +
                    " -Direction Inbound -Action Block -Enabled True -Profile Any" +
                    " -Protocol " + proto +
                    " -RemoteAddress " + ipList + " | Out-Null");
            }
        }

        (int exitCode, string output) = await RunPowerShellAsync(sb.ToString());
        if (exitCode != 0)
        {
            throw new Exception("PowerShell exited with code " + exitCode + ": " + Truncate(output, 400));
        }
    }

    public static async Task<(int Found, int Expected)> VerifyRulesAsync(List<string> blockedCodes)
    {
        if (blockedCodes.Count == 0) return (0, 0);

        HashSet<string> present = await GetBlockedPopsAsync();
        int found = blockedCodes.Count(c => present.Contains(c));
        return (found, blockedCodes.Count);
    }

    private const string SelfTestRuleName = RulePrefix + "selftest";

    public static async Task AddSelfTestBlockAsync(string ip)
    {
        string script =
            "Get-NetFirewallRule -DisplayName '" + SelfTestRuleName + "' -ErrorAction SilentlyContinue | Remove-NetFirewallRule; " +
            "New-NetFirewallRule -DisplayName '" + SelfTestRuleName + "'" +
            " -Description 'CS2 Region Picker - temporary firewall self-test'" +
            " -Direction Outbound -Action Block -Enabled True -Profile Any" +
            " -Protocol TCP -RemoteAddress '" + Sanitize(ip) + "' | Out-Null";
        await RunPowerShellAsync(script);
    }

    public static async Task RemoveSelfTestBlockAsync()
    {
        await RunPowerShellAsync(
            "Get-NetFirewallRule -DisplayName '" + SelfTestRuleName + "' -ErrorAction SilentlyContinue | Remove-NetFirewallRule");
    }

    private static async Task<(int ExitCode, string Output)> RunPowerShellAsync(string script)
    {
        string file = Path.Combine(Path.GetTempPath(), "cs2regionpicker_" + Guid.NewGuid().ToString("N") + ".ps1");
        await File.WriteAllTextAsync(file, script);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + file + "\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using Process process = Process.Start(psi) ?? throw new Exception("Failed to start powershell.exe");

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return (process.ExitCode, await stdoutTask + await stderrTask);
        }
        finally
        {
            try { File.Delete(file); } catch {  }
        }
    }

    private static string Sanitize(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            if (char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_')
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static string Truncate(string value, int max)
    {
        return value.Length <= max ? value : value.Substring(0, max) + "...";
    }
}
