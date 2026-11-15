using System;
using System.Collections.Concurrent;

namespace GtkSharp.Tests
{
    /// <summary>
    /// Lets an external runner own the thread Gtk runs on.
    /// </summary>
    /// <remarks>
    /// On macOS the only GDK backend is Quartz, which requires Gtk — including
    /// NSWindow instantiation and main-loop polling — to run on thread 0, the
    /// process's first thread. Under <c>dotnet test</c> that thread belongs to
    /// the test host, so <see cref="GtkFixture"/> runs Gtk on a background thread
    /// and macOS aborts the moment a toplevel is realized.
    ///
    /// The macOS CI entry point (GtkSharp.Tests.MacRunner) instead owns thread 0
    /// itself: it sets <see cref="Queue"/>, runs xunit on a background thread, and
    /// calls <see cref="RunLoop"/> here on thread 0. When the queue is set,
    /// <see cref="GtkFixture"/> marshals every test body onto it rather than
    /// spawning its own thread, so Gtk always runs on thread 0.
    ///
    /// On Windows and Linux the queue stays null and the fixture keeps its
    /// background-thread behaviour unchanged.
    /// </remarks>
    internal static class GtkThreadBridge
    {
        /// <summary>Set by the runner; null under <c>dotnet test</c>.</summary>
        public static BlockingCollection<Action> Queue;

        /// <summary>
        /// Consumes queued Gtk work on the calling thread until the queue is
        /// completed, draining the main loop after each item. The runner calls
        /// this on thread 0.
        /// </summary>
        public static void RunLoop()
        {
            foreach (var work in Queue.GetConsumingEnumerable())
            {
                // Each work item already captures its own body's exceptions
                // (GtkFixture.Invoke) and releases its waiter in a finally, so a
                // throw out of work() here is a stray one -- a native callback
                // from an async op a skipped test left pending, firing during a
                // later pump. Swallow it so one does not abort the whole run.
                try { work(); }
                catch (Exception e)
                {
                    Console.Error.WriteLine("MacRunner: ignored a stray Gtk-thread exception: " + e.Message);
                }

                // No between-test drain here, unlike the owned-thread fixture.
                // On the macOS Quartz backend Application.EventsPending blocks
                // whenever a shown window still has a frame clock with a pending
                // frame the headless server never presents, which would wedge
                // thread 0 between tests. Each test pumps its own loop (through
                // Pump) where it needs to.
            }
        }

        /// <summary>
        /// Runs pending main-loop work without blocking, so a failure in layout,
        /// a draw function or an idle callback is attributed to the test that
        /// caused it. Bounded so a test that queues work forever fails by timing
        /// out rather than hanging the run. Used by the owned-thread fixture on
        /// Windows and Linux; see <see cref="RunLoop"/> for why macOS skips it.
        /// </summary>
        public static void PumpPending()
        {
            for (int i = 0; i < 200 && Gtk.Application.EventsPending(); i++)
                Gtk.Application.RunIteration(false);
        }
    }
}
