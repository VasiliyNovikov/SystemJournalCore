using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace SystemJournalCore.Tests;

internal static class JournalControl
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromMilliseconds(100);

    public static void Write(Dictionary<string, string> fields)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "logger",
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            ArgumentList = { "--journald" }
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start logger");
        foreach (var (key, value) in fields)
        {
            process.StandardInput.Write(key);
            process.StandardInput.Write('=');
            process.StandardInput.Write(value);
            process.StandardInput.Write('\n');
        }
        process.StandardInput.Close();
        WaitOrKill(process);

        if (process.ExitCode != 0)
        {
            var stderr = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"logger exited with code {process.ExitCode}: {stderr}");
        }
    }

    public static List<Dictionary<string, string>> Read(string? identifier = null, DateTime? since = null, int? lines = null, IEnumerable<string>? matches = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "journalctl",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            ArgumentList = { "--output", "json", "--no-pager" }
        };

        if (identifier is not null)
        {
            startInfo.ArgumentList.Add("--identifier");
            startInfo.ArgumentList.Add(identifier);
        }
        if (since is not null)
        {
            startInfo.ArgumentList.Add("--since");
            startInfo.ArgumentList.Add(since.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }
        if (lines is not null)
        {
            startInfo.ArgumentList.Add("--lines");
            startInfo.ArgumentList.Add(lines.Value.ToString(CultureInfo.InvariantCulture));
        }
        if (matches is not null)
        {
            foreach (var match in matches)
                startInfo.ArgumentList.Add(match);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start journalctl");
        var output = process.StandardOutput.ReadToEnd();
        WaitOrKill(process);

        if (process.ExitCode != 0)
        {
            var stderr = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"journalctl exited with code {process.ExitCode}: {stderr}");
        }

        var entries = new List<Dictionary<string, string>>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var jsonFields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line);
            if (jsonFields is null)
                continue;

            var entry = new Dictionary<string, string>(jsonFields.Count);
            foreach (var (key, value) in jsonFields)
            {
                if (value.ValueKind == JsonValueKind.String)
                    entry[key] = value.GetString()!;
            }
            entries.Add(entry);
        }
        return entries;
    }

    private static void WaitOrKill(Process process)
    {
        if (!process.WaitForExit(ProcessTimeout))
        {
            process.Kill();
            throw new TimeoutException($"Process '{process.StartInfo.FileName}' did not exit within {ProcessTimeout.TotalMilliseconds} milliseconds");
        }
    }
}
