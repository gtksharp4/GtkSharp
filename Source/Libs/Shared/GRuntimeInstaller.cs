using System;

// Fetches a Gtk 4 runtime on demand when none is installed -- the Unix analogue
// of the Windows build-time download in Source/Libs/GtkSharp/GtkSharp.targets.
// Called by GLibrary.TryGet only when a core library fails to load, only on
// Unix, and at most once per process. macOS uses Homebrew here; Linux is
// handled by GLinuxRuntime. The decisions both rely on are the pure methods in
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
            _userLibDir = System.IO.Path.Combine(RuntimeDir(), "env", "lib");
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
                    _succeeded = GLinuxRuntime.Ensure(RuntimeDir());
            }
            catch (Exception e)
            {
                GProcess.Log("runtime install failed: " + e.Message);
                _succeeded = false;
            }

            return _succeeded;
        }
    }

    internal static string RuntimeDir() => GRuntimeProvisioning.ResolveRuntimeDir(
        Environment.GetEnvironmentVariable("XDG_DATA_HOME"),
        Environment.GetEnvironmentVariable("HOME"), GtkVersion);

    static bool EnsureOnMac()
    {
        var packages = GRuntimeProvisioning.BrewPackages(
            Environment.GetEnvironmentVariable(GRuntimeProvisioning.BrewPackagesEnvVar));

        var brew = GProcess.Which("brew");
        if (brew == null)
        {
            GProcess.Log("Gtk 4 is missing and Homebrew was not found. Install it with: brew install "
                         + string.Join(" ", packages));
            return false;
        }

        GProcess.Log("Gtk 4 is missing; installing it with Homebrew (" + string.Join(" ", packages)
                     + ", first run only)...");

        var args = new string[packages.Length + 1];
        args[0] = "install";
        Array.Copy(packages, 0, args, 1, packages.Length);
        return GProcess.Run(brew, args);
    }
}
