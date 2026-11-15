using System;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading;
using Gtk;
using Xunit;

namespace GtkSharp.Tests
{
    /// <summary>
    /// Owns the single thread that Gtk is initialised on, and marshals test
    /// bodies onto it.
    /// </summary>
    /// <remarks>
    /// Gtk may only be used from the thread that called gtk_init, and xunit
    /// makes no promise about which thread a test body runs on. Disabling
    /// parallelism is necessary but not sufficient: sequential tests can still
    /// land on different pool threads. So the fixture starts one thread,
    /// initialises Gtk there, and runs every test body on it through
    /// <see cref="Invoke"/>.
    ///
    /// Each work item is followed by draining the pending main-loop work, so
    /// that a failure in layout, a draw function or an idle callback is
    /// attributed to the test that caused it rather than to whichever test
    /// happens to run next.
    ///
    /// When an external runner has set <see cref="GtkThreadBridge.Queue"/> (the
    /// macOS CI runner, which must put Gtk on thread 0), the fixture marshals
    /// onto that queue instead of starting a thread. See
    /// <see cref="GtkThreadBridge"/>.
    /// </remarks>
    public sealed class GtkFixture : IDisposable
    {
        private readonly BlockingCollection<Action> _queue;
        private readonly Thread _thread;        // null when a runner owns the Gtk thread

        public GtkFixture()
        {
            ExceptionDispatchInfo startupError = null;

            if (GtkThreadBridge.Queue != null)
            {
                // A runner owns thread 0 and pumps the queue; just initialise Gtk
                // on it. No thread of our own, and Dispose must not complete the
                // shared queue -- the runner's lifecycle owns it.
                _queue = GtkThreadBridge.Queue;
                Marshal(() =>
                {
                    try { Application.Init(); }
                    catch (Exception e) { startupError = ExceptionDispatchInfo.Capture(e); }
                });
                startupError?.Throw();
                return;
            }

            var ready = new ManualResetEventSlim();
            _queue = new BlockingCollection<Action>();

            _thread = new Thread(() =>
            {
                try
                {
                    Application.Init();
                }
                catch (Exception e)
                {
                    startupError = ExceptionDispatchInfo.Capture(e);
                    ready.Set();
                    return;
                }

                ready.Set();

                foreach (var work in _queue.GetConsumingEnumerable())
                {
                    work();
                    GtkThreadBridge.PumpPending();
                }
            });

            _thread.IsBackground = true;
            _thread.Name = "Gtk";
            if (OperatingSystem.IsWindows())
                _thread.SetApartmentState(ApartmentState.STA);

            _thread.Start();
            ready.Wait();

            startupError?.Throw();
        }

        /// <summary>Runs <paramref name="action"/> on the Gtk thread, rethrowing anything it throws.</summary>
        public void Invoke(Action action)
        {
            ExceptionDispatchInfo error = null;
            Marshal(() =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    error = ExceptionDispatchInfo.Capture(e);
                }
            });

            error?.Throw();
        }

        public T Invoke<T>(Func<T> func)
        {
            T result = default;
            Invoke(() => { result = func(); });
            return result;
        }

        /// <summary>Enqueues work on the Gtk thread and blocks until it has run.</summary>
        private void Marshal(Action work)
        {
            using var done = new ManualResetEventSlim();
            _queue.Add(() =>
            {
                try { work(); }
                finally { done.Set(); }
            });

            done.Wait();
        }

        public void Dispose()
        {
            if (_thread == null)
                return;   // the runner owns the shared queue and the thread.

            _queue.CompleteAdding();
            _thread.Join(TimeSpan.FromSeconds(10));
        }
    }

    [CollectionDefinition(Name)]
    public sealed class GtkCollection : ICollectionFixture<GtkFixture>
    {
        public const string Name = "Gtk";
    }

    /// <summary>Base class that routes each test body onto the Gtk thread.</summary>
    [Collection(GtkCollection.Name)]
    public abstract class GtkTestBase
    {
        protected GtkTestBase(GtkFixture fixture)
        {
            // Deliberately not called "Gtk": that would shadow the Gtk namespace
            // inside every derived test.
            Fixture = fixture;
        }

        protected GtkFixture Fixture { get; }

        /// <summary>Runs a test body on the Gtk thread.</summary>
        protected void Run(Action body) => Fixture.Invoke(body);

        protected T Run<T>(Func<T> body) => Fixture.Invoke(body);
    }
}
