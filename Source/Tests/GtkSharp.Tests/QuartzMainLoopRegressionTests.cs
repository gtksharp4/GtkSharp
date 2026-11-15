using Gtk;
using Xunit;

namespace GtkSharp.Tests
{
    /// <summary>
    /// Pins the contract the macOS Quartz backend violated: a toplevel must be
    /// realizable and the main loop iterable from the thread Gtk runs on.
    /// </summary>
    /// <remarks>
    /// GDK-Quartz replaces the main-context poll function with one that calls
    /// Cocoa's <c>nextEventMatchingMask</c>, and <c>NSWindow</c> instantiation is
    /// likewise main-thread-only -- both abort off thread 0. Because
    /// <see cref="GtkFixture"/> runs Gtk on a background thread under
    /// <c>dotnet test</c>, presenting a window used to crash the whole test host
    /// on macOS. The macOS CI entry point runs Gtk on thread 0 instead (see
    /// <see cref="GtkThreadBridge"/>).
    ///
    /// On Windows and X11 this is a harmless smoke test; on macOS it is the
    /// regression guard. It reproduces the crash path: open a display-backed
    /// window, then pump the loop.
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
                    // which is where Quartz instantiates its NSWindow.
                    window.Present();

                    for (int i = 0; i < 200 && Application.EventsPending(); i++)
                        Application.RunIteration(false);
                }
                finally
                {
                    window.Destroy();
                }

                // Reaching here means the window realized and the loop iterated
                // without aborting the host.
                Assert.True(true);
            });
        }
    }
}
