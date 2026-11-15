using System;
using System.Runtime.InteropServices;

namespace GtkSharp.Tests
{
    /// <summary>
    /// macOS-only: replaces the default GLib main-context poll function with the
    /// plain <c>g_poll</c>, undoing GDK-Quartz's substitution.
    /// </summary>
    /// <remarks>
    /// GDK's Quartz backend (the only one Homebrew's gtk4 builds: it is
    /// configured <c>-Dmacos-backend=true -Dx11-backend=false</c>, and GTK's
    /// broadway backend is off by default) swaps the main context's poll function
    /// for one that calls Cocoa's <c>nextEventMatchingMask</c>. That API aborts
    /// unless it runs on thread 0 ("nextEventMatchingMask should only be called
    /// from the Main Thread!").
    ///
    /// <see cref="GtkFixture"/> runs Gtk on a dedicated background thread, so the
    /// first main-loop iteration after a <c>GdkDisplay</c> is opened crashes the
    /// test host. The suite injects no real input events -- it drives widgets,
    /// idles and timeouts -- so restoring <c>g_poll</c> lets the loop iterate off
    /// the main thread without touching AppKit, and GLib sources still fire.
    ///
    /// The symbols are resolved with <c>dlsym(RTLD_DEFAULT, ...)</c> because
    /// <c>Application.Init</c> has already loaded libglib globally; this keeps the
    /// workaround self-contained in the test project, which deliberately does not
    /// link the side-effecting GLibrary/GRuntimeInstaller loader.
    /// </remarks>
    internal static class MacQuartzMainLoop
    {
        // RTLD_DEFAULT: dlsym searches every globally-loaded image in load order.
        private static readonly IntPtr RtldDefault = new IntPtr(-2);

        [DllImport("/usr/lib/libSystem.dylib")]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetPollFunc(IntPtr context, IntPtr func);

        /// <summary>
        /// Points the default main context's poll function back at <c>g_poll</c>.
        /// Call once, on the Gtk thread, after <c>Application.Init</c>.
        /// </summary>
        public static void RestoreDefaultPollFunc()
        {
            var gPoll = dlsym(RtldDefault, "g_poll");
            var setter = dlsym(RtldDefault, "g_main_context_set_poll_func");

            if (gPoll == IntPtr.Zero || setter == IntPtr.Zero)
                throw new InvalidOperationException(
                    "Could not resolve g_poll / g_main_context_set_poll_func from the loaded libglib; " +
                    "the macOS Quartz main-loop workaround cannot be applied.");

            var setPollFunc = Marshal.GetDelegateForFunctionPointer<SetPollFunc>(setter);
            setPollFunc(GLib.MainContext.Default.Handle, gPoll);
        }
    }
}
