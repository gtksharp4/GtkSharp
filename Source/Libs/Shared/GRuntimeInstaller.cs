using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;

// Fetches a Gtk 4 runtime on demand when none is installed -- the Unix analogue
// of the Windows build-time download in Source/Libs/GtkSharp/GtkSharp.targets.
// Called by GLibrary.TryGet only when a core library fails to load, only on
// Unix, and at most once per process. macOS uses Homebrew; Linux pulls a
// conda-forge build with micromamba, falling back to the distro package
// manager. The decisions it relies on are the pure methods in
// GRuntimeProvisioning, which the tests cover directly.
static class GRuntimeInstaller
{
    // Keep in step with GtkDir in Source/Libs/GtkSharp/GtkSharp.targets and the
    // bin path in GLibrary's Windows branch.
    const string GtkVersion = "4.22.4";

    static readonly object _gate = new object();
    static bool _attempted;
    static bool _succeeded;
    static string _userLibDir;

    // Directory the Linux conda runtime is unpacked into, or null off Linux.
    // GLibrary loads libgtk-4.so.1 from here by full path, no LD_LIBRARY_PATH.
    public static string UserLibDir
    {
        get
        {
            if (_userLibDir != null)
                return _userLibDir;
            if (!FuncLoader.IsLinux)
                return null;
            _userLibDir = Path.Combine(RuntimeDir(), "env", "lib");
            return _userLibDir;
        }
    }

    public static bool TryEnsureRuntime()
    {
        lock (_gate)
        {
            if (_attempted)
                return _succeeded;
            _attempted = true;

            if (GRuntimeProvisioning.IsInstallSkipped(
                    Environment.GetEnvironmentVariable(GRuntimeProvisioning.SkipEnvVar)))
                return _succeeded = false;

            try
            {
                if (FuncLoader.IsOSX)
                    _succeeded = EnsureOnMac();
                else if (FuncLoader.IsLinux)
                    _succeeded = EnsureOnLinux();
            }
            catch (Exception e)
            {
                Log("runtime install failed: " + e.Message);
                _succeeded = false;
            }

            return _succeeded;
        }
    }

    static string RuntimeDir() => GRuntimeProvisioning.ResolveRuntimeDir(
        Environment.GetEnvironmentVariable("XDG_DATA_HOME"),
        Environment.GetEnvironmentVariable("HOME"), GtkVersion);

    static bool EnsureOnMac()
    {
        var brew = Which("brew");
        if (brew == null)
        {
            Log("Gtk 4 is missing and Homebrew was not found. Install it with: brew install gtk4");
            return false;
        }

        Log("Gtk 4 is missing; installing it with Homebrew (first run only)...");
        return Run(brew, new[] { "install", "gtk4" });
    }

    static bool EnsureOnLinux()
    {
        var runtimeDir = RuntimeDir();
        var envPrefix = Path.Combine(runtimeDir, "env");
        var gtkSo = Path.Combine(envPrefix, "lib", "libgtk-4.so.1");

        if (File.Exists(gtkSo))
            return true;

        if (TryCondaInstall(runtimeDir, envPrefix) && File.Exists(gtkSo))
            return true;

        Log("conda-forge runtime unavailable; trying the distro package manager (may prompt for your password)...");
        return TryPackageManagerInstall();
    }

    static bool TryCondaInstall(string runtimeDir, string envPrefix)
    {
        var url = Environment.GetEnvironmentVariable("GTKSHARP_MICROMAMBA_URL")
                  ?? GRuntimeProvisioning.MicromambaUrl(RuntimeInformation.OSArchitecture);
        if (string.IsNullOrEmpty(url))
        {
            Log("no micromamba build for this CPU architecture.");
            return false;
        }

        var bin = Path.Combine(runtimeDir, "bin", "micromamba");
        if (!File.Exists(bin))
        {
            Log("downloading micromamba to bootstrap the Gtk 4 runtime...");
            Directory.CreateDirectory(Path.GetDirectoryName(bin));
            DownloadFile(url, bin);
            Run("chmod", new[] { "+x", bin });
        }

        var channel = Environment.GetEnvironmentVariable("GTKSHARP_CONDA_CHANNEL") ?? "conda-forge";
        Log("fetching the Gtk 4 runtime from " + channel + " (first run only, a few hundred MB)...");
        return Run(bin, new[]
        {
            "create", "--yes",
            "--root-prefix", runtimeDir,
            "--prefix", envPrefix,
            "--channel", channel,
            "gtk4"
        });
    }

    static bool TryPackageManagerInstall()
    {
        string id = null;
        try
        {
            if (File.Exists("/etc/os-release"))
                id = GRuntimeProvisioning.ParseOsReleaseId(File.ReadAllText("/etc/os-release"));
        }
        catch { }

        var cmd = GRuntimeProvisioning.PackageManagerCommand(id);
        if (cmd == null)
        {
            Log("unknown distro; install the Gtk 4 runtime with your package manager (e.g. libgtk-4-1).");
            return false;
        }

        var elevator = Which("pkexec") ?? Which("sudo");
        if (elevator == null)
        {
            var rest = new string[cmd.Length - 1];
            Array.Copy(cmd, 1, rest, 0, rest.Length);
            return Run(cmd[0], rest); // already running as root
        }

        return Run(elevator, cmd);
    }

    static void DownloadFile(string url, string dest)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        using var source = client.GetStreamAsync(url).GetAwaiter().GetResult();
        using var file = File.Create(dest);
        source.CopyTo(file);
    }

    static bool Run(string exe, string[] args)
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

    static string Which(string exe)
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

    static void Log(string message) => Console.Error.WriteLine("[GtkSharp] " + message);
}
