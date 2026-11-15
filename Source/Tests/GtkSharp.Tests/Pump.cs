using System;
using System.Diagnostics;
using Xunit;

namespace GtkSharp.Tests
{
    /// <summary>
    /// Main-loop pumping that stays responsive on the macOS Quartz backend.
    /// </summary>
    /// <remarks>
    /// A deadline loop that pumps with <c>Application.EventsPending</c> /
    /// <c>RunIteration(false)</c> can wedge the Gtk thread on the headless macOS
    /// runner: those calls, non-blocking on X11 and win32, block inside the
    /// Quartz event loop, so the <see cref="Stopwatch"/> deadline is never
    /// re-checked. A <em>blocking</em> iteration bounded by a short recurring
    /// timeout is the shape that returns promptly everywhere -- the timeout is
    /// always ready within its interval, so the iteration cannot wait past it.
    /// This is the same trick <c>DesktopIntegrationTests.PumpUntil</c> uses; it
    /// is collected here so the unconditional pump loops can share it.
    /// </remarks>
    internal static class Pump
    {
        // Iterating the main loop blocks on the headless macOS runner (Quartz
        // waits on a frame the window server never presents), so a caller that
        // needs the loop pumped cannot run there.
        const string MacSkip = "headless macOS cannot pump the main loop (Quartz frame-clock block).";

        /// <summary>Pumps the main loop for <paramref name="milliseconds"/>.</summary>
        public static void For(int milliseconds)
        {
            Skip.If(TestEnvironment.IsMacOS, MacSkip);

            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < milliseconds)
            {
                GLib.MainContext.Iteration(false);
                System.Threading.Thread.Sleep(1);
            }
        }

        /// <summary>
        /// Pumps until <paramref name="done"/> or the deadline; returns the final
        /// value of <paramref name="done"/>.
        /// </summary>
        public static bool Until(Func<bool> done, int milliseconds = 5000)
        {
            Skip.If(TestEnvironment.IsMacOS, MacSkip);

            var clock = Stopwatch.StartNew();
            while (!done() && clock.ElapsedMilliseconds < milliseconds)
            {
                GLib.MainContext.Iteration(false);
                System.Threading.Thread.Sleep(1);
            }

            return done();
        }
    }
}
