using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;

// Running external processes and downloading files for the on-demand runtime
// install (GRuntimeInstaller / GLinuxRuntime). Isolated here so the installers
// read as policy and this reads as mechanism.
static class GProcess
{
    public static void DownloadFile(string url, string dest)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        using var source = client.GetStreamAsync(url).GetAwaiter().GetResult();
        using var file = File.Create(dest);
        source.CopyTo(file);
    }

    public static bool Run(string exe, string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = Quote(args),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(psi);
        if (process == null)
            return false;

        // Read stdout asynchronously so a full pipe buffer cannot deadlock the
        // child against our synchronous stderr read.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var err = process.StandardError.ReadToEnd();
        process.WaitForExit();
        stdout.GetAwaiter().GetResult();

        if (process.ExitCode != 0 && err.Length > 0)
            Log(err.Trim());

        return process.ExitCode == 0;
    }

    // ProcessStartInfo.ArgumentList is netstandard2.1+, so quote into the single
    // Arguments string by hand. Our args are simple; double-quoting each is
    // enough for the Unix argv parser .NET applies.
    static string Quote(string[] args)
    {
        var sb = new StringBuilder();
        foreach (var a in args)
        {
            if (sb.Length > 0)
                sb.Append(' ');
            sb.Append('"').Append(a.Replace("\"", "\\\"")).Append('"');
        }
        return sb.ToString();
    }

    public static string Which(string exe)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (path != null)
            foreach (var dir in path.Split(Path.PathSeparator))
            {
                try
                {
                    var full = Path.Combine(dir, exe);
                    if (File.Exists(full))
                        return full;
                }
                catch { }
            }

        // A GUI app's PATH often omits these, where Homebrew puts its shims.
        foreach (var candidate in new[] { "/opt/homebrew/bin/" + exe, "/usr/local/bin/" + exe })
            if (File.Exists(candidate))
                return candidate;

        return null;
    }

    public static void Log(string message) => Console.Error.WriteLine("[GtkSharp] " + message);
}
