using System;
using System.IO;
using System.Runtime.InteropServices;

// The Linux half of the on-demand Gtk 4 runtime install. A single conda-forge
// gtk4 download does not bring its dependency closure, so the primary path
// downloads the micromamba static binary and lets it resolve the whole closure
// into a user-cache dir (relocatable, nothing to host); it falls back to the
// distro package manager. Called only by GRuntimeInstaller, only on Linux.
static class GLinuxRuntime
{
    public static bool Ensure(string runtimeDir)
    {
        var envPrefix = Path.Combine(runtimeDir, "env");
        var gtkSo = Path.Combine(envPrefix, "lib", "libgtk-4.so.1");

        if (File.Exists(gtkSo))
            return true;

        if (TryConda(runtimeDir, envPrefix) && File.Exists(gtkSo))
            return true;

        GProcess.Log("conda-forge runtime unavailable; trying the distro package manager (may prompt for your password)...");
        return TryPackageManager();
    }

    static bool TryConda(string runtimeDir, string envPrefix)
    {
        var url = Environment.GetEnvironmentVariable("GTKSHARP_MICROMAMBA_URL")
                  ?? GRuntimeProvisioning.MicromambaUrl(RuntimeInformation.OSArchitecture);
        if (string.IsNullOrEmpty(url))
        {
            GProcess.Log("no micromamba build for this CPU architecture.");
            return false;
        }

        var bin = Path.Combine(runtimeDir, "bin", "micromamba");
        if (!File.Exists(bin))
        {
            GProcess.Log("downloading micromamba to bootstrap the Gtk 4 runtime...");
            Directory.CreateDirectory(Path.GetDirectoryName(bin));
            GProcess.DownloadFile(url, bin);
            GProcess.Run("chmod", new[] { "+x", bin });
        }

        var channel = Environment.GetEnvironmentVariable("GTKSHARP_CONDA_CHANNEL") ?? "conda-forge";
        GProcess.Log("fetching the Gtk 4 runtime from " + channel + " (first run only, a few hundred MB)...");
        return GProcess.Run(bin, new[]
        {
            "create", "--yes",
            "--root-prefix", runtimeDir,
            "--prefix", envPrefix,
            "--channel", channel,
            "gtk4"
        });
    }

    static bool TryPackageManager()
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
            GProcess.Log("unknown distro; install the Gtk 4 runtime with your package manager (e.g. libgtk-4-1).");
            return false;
        }

        var elevator = GProcess.Which("pkexec") ?? GProcess.Which("sudo");
        if (elevator == null)
        {
            var rest = new string[cmd.Length - 1];
            Array.Copy(cmd, 1, rest, 0, rest.Length);
            return GProcess.Run(cmd[0], rest); // already running as root
        }

        return GProcess.Run(elevator, cmd);
    }
}
