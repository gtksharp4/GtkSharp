using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace GtkSharp.Tests
{
    /// <summary>
    /// The pure decisions behind the on-demand Unix runtime install
    /// (GRuntimeProvisioning). The orchestration that downloads and spawns
    /// processes (GRuntimeInstaller) is not reachable from a unit test; these
    /// pin the logic it delegates to. GRuntimeProvisioning.cs and Library.cs are
    /// linked into this project (see GtkSharp.Tests.csproj) because the wrapper
    /// assemblies are strong-named and this one is not, ruling out
    /// InternalsVisibleTo. No Gtk runtime is needed, so this is not a GtkTestBase.
    /// </summary>
    public class RuntimeProvisioningTests
    {
        // Library is internal, so it cannot appear in a public [Theory] signature
        // (CS0051); assert over the values inside the body instead.
        [Fact]
        public void Core_libraries_trigger_install()
        {
            Assert.True(GRuntimeProvisioning.IsCoreLibrary(Library.Gtk));
            Assert.True(GRuntimeProvisioning.IsCoreLibrary(Library.GLib));
            Assert.True(GRuntimeProvisioning.IsCoreLibrary(Library.Gdk));
            Assert.True(GRuntimeProvisioning.IsCoreLibrary(Library.Gsk));
            Assert.True(GRuntimeProvisioning.IsCoreLibrary(Library.Cairo));
        }

        [Fact]
        public void Optional_libraries_do_not_trigger_install()
        {
            // Callers gate these with GLibrary.IsSupported; a missing one must
            // not drag down a few hundred MB of runtime.
            Assert.False(GRuntimeProvisioning.IsCoreLibrary(Library.Webkit));
            Assert.False(GRuntimeProvisioning.IsCoreLibrary(Library.JavaScriptCore));
            Assert.False(GRuntimeProvisioning.IsCoreLibrary(Library.GtkSource));
            Assert.False(GRuntimeProvisioning.IsCoreLibrary(Library.Adwaita));
        }

        [Theory]
        [InlineData("1", true)]
        [InlineData("true", true)]
        [InlineData("TRUE", true)]
        [InlineData("0", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Skip_env_var_is_honoured(string value, bool skipped)
        {
            Assert.Equal(skipped, GRuntimeProvisioning.IsInstallSkipped(value));
        }

        [Fact]
        public void Runtime_dir_prefers_xdg_data_home()
        {
            var dir = GRuntimeProvisioning.ResolveRuntimeDir("/xdg", "/home/u", "4.22.4");
            Assert.Equal(Path.Combine("/xdg", "GtkSharp", "4.22.4"), dir);
        }

        [Fact]
        public void Runtime_dir_falls_back_to_local_share()
        {
            var dir = GRuntimeProvisioning.ResolveRuntimeDir(null, "/home/u", "4.22.4");
            Assert.Equal(Path.Combine("/home/u", ".local", "share", "GtkSharp", "4.22.4"), dir);
        }

        [Theory]
        [InlineData(Architecture.X64, "micromamba-linux-64")]
        [InlineData(Architecture.Arm64, "micromamba-linux-aarch64")]
        public void Micromamba_asset_matches_cpu(Architecture arch, string asset)
        {
            Assert.Equal(asset, GRuntimeProvisioning.MicromambaAssetName(arch));
            Assert.EndsWith(asset, GRuntimeProvisioning.MicromambaUrl(arch));
        }

        [Fact]
        public void Micromamba_is_null_for_unsupported_cpu()
        {
            Assert.Null(GRuntimeProvisioning.MicromambaAssetName(Architecture.X86));
            Assert.Null(GRuntimeProvisioning.MicromambaUrl(Architecture.X86));
        }

        [Theory]
        [InlineData("ubuntu", "apt-get")]
        [InlineData("debian", "apt-get")]
        [InlineData("fedora", "dnf")]
        [InlineData("arch", "pacman")]
        [InlineData("opensuse-tumbleweed", "zypper")]
        public void Package_manager_command_maps_known_distros(string id, string exe)
        {
            var cmd = GRuntimeProvisioning.PackageManagerCommand(id);
            Assert.NotNull(cmd);
            Assert.Equal(exe, cmd[0]);
        }

        [Fact]
        public void Package_manager_command_is_null_for_unknown_distro()
        {
            Assert.Null(GRuntimeProvisioning.PackageManagerCommand("plan9"));
        }

        [Fact]
        public void Os_release_id_is_read_directly_when_known()
        {
            Assert.Equal("fedora", GRuntimeProvisioning.ParseOsReleaseId("NAME=Fedora\nID=fedora\n"));
        }

        [Fact]
        public void Os_release_id_is_unquoted()
        {
            Assert.Equal("ubuntu", GRuntimeProvisioning.ParseOsReleaseId("ID=\"ubuntu\"\n"));
        }

        [Fact]
        public void Os_release_falls_back_to_a_known_id_like_token()
        {
            // Linux Mint: ID is unknown to us, but ID_LIKE names its base.
            var id = GRuntimeProvisioning.ParseOsReleaseId("ID=linuxmint-unknown\nID_LIKE=\"ubuntu debian\"\n");
            Assert.Equal("ubuntu", id);
        }
    }
}
