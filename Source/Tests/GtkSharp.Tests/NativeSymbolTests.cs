using System;
using System.Runtime.InteropServices;
using Xunit;

namespace GtkSharp.Tests
{
    /// <summary>
    /// What happens when a binding names a native function the loaded library
    /// does not export (G6), and the runtime version check that guards it (G5).
    /// </summary>
    /// <remarks>
    /// A missing export used to bind to a null delegate, so the call died with a
    /// NullReferenceException that named nothing. It now binds to a delegate that
    /// throws a NotSupportedException naming the symbol. The tests bind a symbol
    /// that cannot exist, so they do not depend on the installed Gtk version.
    /// </remarks>
    public class NativeSymbolTests : GtkTestBase
    {
        public NativeSymbolTests(GtkFixture fixture) : base(fixture) { }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate int d_missing_with_args(IntPtr raw, ref int value, string text);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate void d_missing_void();

        const string Missing = "gtksharp_tests_symbol_that_does_not_exist";

        // FuncLoader and GLibrary are internal to the strong-named GLibSharp, and
        // this test assembly is not signed, so InternalsVisibleTo is not an option;
        // reflection reaches them, calling exactly what generated code calls:
        //   FuncLoader.LoadFunction<T>(FuncLoader.GetProcAddress(GLibrary.Load(Library.GLib), name))
        static T Bind<T>(string symbol)
        {
            var assembly = typeof(GLib.Global).Assembly;
            var loader = assembly.GetType("FuncLoader", true);
            var library = Enum.Parse(assembly.GetType("Library", true), "GLib");

            var handle = assembly.GetType("GLibrary", true).GetMethod("Load").Invoke(null, new[] { library });
            var address = loader.GetMethod("GetProcAddress").Invoke(null, new[] { handle, symbol });

            return (T)loader.GetMethod("LoadFunction").MakeGenericMethod(typeof(T)).Invoke(null, new[] { address });
        }

        [Fact]
        public void A_missing_symbol_binds_to_a_delegate_not_to_null()
        {
            Assert.NotNull(Bind<d_missing_void>(Missing));
        }

        [Fact]
        public void Calling_a_missing_symbol_throws_NotSupported_naming_it()
        {
            var function = Bind<d_missing_with_args>(Missing);
            int value = 1;

            var error = Assert.Throws<NotSupportedException>(() => function(IntPtr.Zero, ref value, "x"));

            Assert.Contains(Missing, error.Message);
        }

        [Fact]
        public void A_void_delegate_throws_the_same_way()
        {
            var error = Assert.Throws<NotSupportedException>(() => Bind<d_missing_void>(Missing + "_void")());

            Assert.Contains(Missing + "_void", error.Message);
        }

        [Fact]
        public void A_symbol_that_exists_still_binds_to_the_native_function()
        {
            var function = Bind<d_g_get_prgname>("g_get_prgname");

            // Does not throw; the result may be null when no program name is set.
            function();
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate IntPtr d_g_get_prgname();

        [Fact]
        public void The_name_of_one_missing_symbol_does_not_leak_into_the_next_binding()
        {
            Bind<d_missing_void>(Missing + "_first");
            var second = Bind<d_missing_void>(Missing + "_second");

            var error = Assert.Throws<NotSupportedException>(() => second());

            Assert.Contains(Missing + "_second", error.Message);
            Assert.DoesNotContain(Missing + "_first", error.Message);
        }

        [Fact]
        public void IsSymbolAvailable_tells_exported_from_missing()
        {
            Assert.True(GLib.Global.IsSymbolAvailable("g_free"));
            Assert.True(GLib.Global.IsSymbolAvailable("gtk_init"));
            Assert.False(GLib.Global.IsSymbolAvailable(Missing));
        }

        [Fact]
        public void IsSymbolAvailable_rejects_an_empty_name()
        {
            Assert.Throws<ArgumentException>(() => GLib.Global.IsSymbolAvailable(""));
            Assert.Throws<ArgumentException>(() => GLib.Global.IsSymbolAvailable(null));
        }

        // ------------------------------------------------------------ G5

        [Fact]
        public void GtkVersion_reports_the_gtk_that_is_loaded()
        {
            Assert.Equal(Gtk.Global.MajorVersion, Gtk.GtkVersion.Major);
            Assert.Equal(Gtk.Global.MinorVersion, Gtk.GtkVersion.Minor);
            Assert.Equal(Gtk.Global.MicroVersion, Gtk.GtkVersion.Micro);
            Assert.Equal(new Version((int)Gtk.GtkVersion.Major, (int)Gtk.GtkVersion.Minor, (int)Gtk.GtkVersion.Micro), Gtk.GtkVersion.Current);
        }

        [Fact]
        public void AtLeast_is_inclusive_of_the_loaded_version()
        {
            var major = Gtk.GtkVersion.Major;
            var minor = Gtk.GtkVersion.Minor;
            var micro = Gtk.GtkVersion.Micro;

            Assert.True(Gtk.GtkVersion.AtLeast(major, minor, micro));
            Assert.True(Gtk.GtkVersion.AtLeast(major, minor));
            Assert.True(Gtk.GtkVersion.AtLeast(4, 0));
        }

        [Fact]
        public void AtLeast_rejects_anything_newer_on_any_component()
        {
            var major = Gtk.GtkVersion.Major;
            var minor = Gtk.GtkVersion.Minor;
            var micro = Gtk.GtkVersion.Micro;

            Assert.False(Gtk.GtkVersion.AtLeast(major, minor, micro + 1));
            Assert.False(Gtk.GtkVersion.AtLeast(major, minor + 1));
            Assert.False(Gtk.GtkVersion.AtLeast(major + 1, 0));
        }

        [Fact]
        public void AtLeast_minor_does_not_hide_behind_a_larger_micro()
        {
            // 4.22.0 is older than 4.23.0 whatever the micro numbers are; a
            // component-wise comparison would get this wrong.
            Assert.False(Gtk.GtkVersion.AtLeast(Gtk.GtkVersion.Major, Gtk.GtkVersion.Minor + 1, 0));
        }

        [Fact]
        public void The_test_helper_agrees_with_the_public_check()
        {
            Assert.Equal(Gtk.GtkVersion.AtLeast(4, 22), TestEnvironment.GtkAtLeast(4, 22));
            Assert.Equal(Gtk.GtkVersion.AtLeast(4, 14), TestEnvironment.GtkAtLeast(4, 14));
        }
    }
}
