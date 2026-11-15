using Gtk;
using Xunit;

namespace GtkSharp.Tests
{
    /// <summary>
    /// Pins the contract the macOS Quartz backend violated: once a
    /// <c>GdkDisplay</c> is open, the GLib main loop must be iterable from the
    /// thread <see cref="GtkFixture"/> runs Gtk on.
    /// </summary>
    /// <remarks>
    /// GDK-Quartz replaces the main-context poll function with one that calls
    /// Cocoa's <c>nextEventMatchingMask</c>, which aborts off thread 0. Because
    /// the fixture runs Gtk on a background thread, the first
    /// <c>EventsPending</c>/<c>RunIteration</c> after a window opened used to
    /// crash the whole test host on macOS -- the 24 pure tests that never open a
    /// display passed, then the run aborted. <see cref="MacQuartzMainLoop"/>
    /// restores <c>g_poll</c> to undo that substitution.
    ///
    /// On Windows and X11 this is a harmless smoke test; on macOS it is the
    /// regression guard. It cannot assert the poll function directly (GLib
    /// exposes no getter), so it reproduces the crash path instead: open a
    /// display-backed window, then pump the loop.
    /// </remarks>
    public class QuartzMainLoopRegressionTests : GtkTestBase
    {
        public QuartzMainLoopRegressionTests(GtkFixture fixture) : base(fixture) { }

        [Fact]
        public void Iterating_the_loop_after_opening_a_window_does_not_crash()
        {
            Run(() =>
            {
                var window = new Window { Title = "quartz-main-loop" };
                try
                {
                    // Presenting opens/realizes against the default GdkDisplay,
                    // which is where Quartz attaches its event source.
                    window.Present();

                    for (int i = 0; i < 200 && Application.EventsPending(); i++)
                        Application.RunIteration(false);
                }
                finally
                {
                    window.Destroy();
                }

                // Reaching here means the loop iterated without aborting the host.
                Assert.True(true);
            });
        }
    }
}
