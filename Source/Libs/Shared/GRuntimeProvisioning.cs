using System;
using System.IO;
using System.Runtime.InteropServices;

// Pure, side-effect-free decisions for provisioning a Gtk 4 runtime on Unix.
// Kept apart from GRuntimeInstaller (which downloads, chmods and spawns
// processes) so the test project can link THIS ONE FILE and exercise the logic
// directly: the wrapper assemblies are strong-named and the test assembly is
// not, so InternalsVisibleTo cannot bridge them. See GtkSharp.Tests.csproj.
static class GRuntimeProvisioning
{
    // Set to 1/true to suppress the on-demand install and let the normal
    // DllNotFoundException surface instead (runtime analogue of SkipGtkInstall).
    public const string SkipEnvVar = "GTKSHARP_SKIP_RUNTIME_INSTALL";

    // Only the always-present core of Gtk is auto-installed. Webkit,
    // JavaScriptCore, GtkSourceView and libadwaita are optional -- callers
    // already gate them with GLibrary.IsSupported -- so a missing one must not
    // trigger a runtime download.
    public static bool IsCoreLibrary(Library library)
    {
        switch (library)
        {
            case Library.Webkit:
            case Library.JavaScriptCore:
            case Library.GtkSource:
            case Library.Adwaita:
                return false;
            default:
                return true;
        }
    }

    public static bool IsInstallSkipped(string envValue) =>
        !string.IsNullOrEmpty(envValue) &&
        (envValue == "1" || string.Equals(envValue, "true", StringComparison.OrdinalIgnoreCase));

    // ~/.local/share/GtkSharp/<version> (honours XDG_DATA_HOME). The caller
    // appends env/lib for the loader and bin/micromamba for the tool.
    public static string ResolveRuntimeDir(string xdgDataHome, string home, string version)
    {
        var dataHome = !string.IsNullOrEmpty(xdgDataHome)
            ? xdgDataHome
            : Path.Combine(home ?? string.Empty, ".local", "share");
        return Path.Combine(dataHome, "GtkSharp", version);
    }

    // The micromamba static binary for this CPU in the micromamba-releases repo,
    // or null when there is no build for the architecture (skip the conda path).
    public static string MicromambaAssetName(Architecture arch)
    {
        switch (arch)
        {
            case Architecture.X64: return "micromamba-linux-64";
            case Architecture.Arm64: return "micromamba-linux-aarch64";
            default: return null;
        }
    }

    public static string MicromambaUrl(Architecture arch)
    {
        var asset = MicromambaAssetName(arch);
        return asset == null
            ? null
            : "https://github.com/mamba-org/micromamba-releases/releases/latest/download/" + asset;
    }

    // The conda-forge packages the on-demand install pulls. Defaults to the Gtk
    // core; GTKSHARP_CONDA_PACKAGES can add companion libraries not fetched by
    // default (e.g. "gtk4 libadwaita"), space-separated.
    public static string[] CondaPackages(string envValue)
    {
        if (string.IsNullOrWhiteSpace(envValue))
            return new[] { "gtk4" };

        return envValue.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
    }

    // Maps an /etc/os-release ID to the command that installs the Gtk 4 runtime
    // package on that distro family. Returns null when the distro is unknown.
    public static string[] PackageManagerCommand(string osReleaseId)
    {
        switch (osReleaseId)
        {
            case "debian":
            case "ubuntu":
            case "linuxmint":
            case "pop":
                return new[] { "apt-get", "install", "-y", "libgtk-4-1" };
            case "fedora":
            case "rhel":
            case "centos":
                return new[] { "dnf", "install", "-y", "gtk4" };
            case "arch":
            case "manjaro":
                return new[] { "pacman", "-S", "--noconfirm", "gtk4" };
            case "opensuse":
            case "opensuse-leap":
            case "opensuse-tumbleweed":
                return new[] { "zypper", "install", "-y", "gtk4" };
            default:
                return null;
        }
    }

    // Reads the ID= value from /etc/os-release contents. When ID is unknown,
    // falls back to the first recognised ID_LIKE token (e.g. Mint's
    // ID_LIKE="ubuntu debian"). May return null or an unknown id; the caller
    // handles that via PackageManagerCommand returning null.
    public static string ParseOsReleaseId(string content)
    {
        if (string.IsNullOrEmpty(content))
            return null;

        string id = null, idLike = null;
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("ID=", StringComparison.Ordinal))
                id = Unquote(line.Substring(3));
            else if (line.StartsWith("ID_LIKE=", StringComparison.Ordinal))
                idLike = Unquote(line.Substring(8));
        }

        if (!string.IsNullOrEmpty(id) && PackageManagerCommand(id) != null)
            return id;

        if (!string.IsNullOrEmpty(idLike))
            foreach (var token in idLike.Split(' '))
                if (PackageManagerCommand(token) != null)
                    return token;

        return id;
    }

    static string Unquote(string value) => value.Trim().Trim('"');
}
