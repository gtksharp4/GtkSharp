using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using GtkSharp.Tests;
using Xunit.Runners;

// macOS test entry point. The Quartz backend requires Gtk -- NSWindow
// instantiation and main-loop polling alike -- to run on thread 0, which the
// dotnet test host owns. This process owns thread 0 itself: it hands GtkFixture
// a queue to marshal onto (see GtkThreadBridge), runs xunit on a background
// thread, and pumps that queue (and the Gtk main loop) here on thread 0 until
// the run completes.
internal static class Program
{
    private static readonly ManualResetEventSlim Finished = new();
    private static volatile int _failed;
    private static volatile int _total;
    private static volatile int _skipped;
    private static volatile bool _errored;

    // Stall detection. A test that blocks the Gtk thread (a modal dialog that
    // waits for a response that never comes on headless macOS, say) would
    // otherwise hang thread 0 silently until the job's wall-clock timeout. The
    // watchdog runs on its own thread, so it fires even while thread 0 is stuck.
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static long _lastActivityTicks;
    private static volatile string _currentTest = "(startup)";
    private static volatile bool _testsStarted;
    private const int StallSeconds = 120;

    // STA so thread 0 matches what Gtk wants on Windows (OleInitialize); a no-op
    // on macOS, where thread 0 is simply the one Quartz requires.
    [STAThread]
    private static int Main()
    {
        // The whole point is to run Gtk on thread 0. If the CLR ever started Main
        // off the macOS main thread, Quartz would abort deep inside Gtk with no
        // clue why -- so fail fast here with one that names the cause.
        if (OperatingSystem.IsMacOS() && pthread_main_np() == 0)
        {
            Console.Error.WriteLine(
                "MacRunner must run on the macOS main thread (thread 0): the Quartz " +
                "backend requires it and this process did not start there.");
            return 2;
        }

        // This process only runs on macOS (test-macos.sh), so mark the headless
        // Quartz environment for TestEnvironment.IsMacOS -- OperatingSystem.IsMacOS
        // is not relied on alone, having come back false here under the runner.
        Environment.SetEnvironmentVariable(TestEnvironment.MacRunnerEnvVar, "1");
        Console.Error.WriteLine(
            $"MacRunner OS check: OperatingSystem.IsMacOS()={OperatingSystem.IsMacOS()}, " +
            $"{RuntimeInformation.OSDescription}");

        GtkThreadBridge.Queue = new BlockingCollection<Action>();

        Touch();
        new Thread(Watchdog) { IsBackground = true, Name = "watchdog" }.Start();

        var assemblyPath = typeof(GtkFixture).Assembly.Location;
        var worker = new Thread(() => RunTests(assemblyPath))
        {
            IsBackground = true,
            Name = "xunit"
        };
        worker.Start();

        // Blocks consuming queued Gtk work on thread 0 until RunTests completes
        // the queue.
        GtkThreadBridge.RunLoop();

        Console.WriteLine($"MacRunner: {_total} tests, {_failed} failed, {_skipped} skipped.");
        return _failed > 0 || _errored ? 1 : 0;
    }

    private static void RunTests(string assemblyPath)
    {
        try
        {
            using var runner = AssemblyRunner.WithoutAppDomain(assemblyPath);

            runner.OnTestStarting = info =>
            {
                _currentTest = info.TestDisplayName;
                _testsStarted = true;
                Touch();
            };
            runner.OnTestFinished = _ => Touch();
            runner.OnTestFailed = info =>
                Console.WriteLine($"FAIL {info.TestDisplayName}: {info.ExceptionMessage}");
            runner.OnErrorMessage = info =>
            {
                _errored = true;
                Console.WriteLine($"ERROR {info.ExceptionType}: {info.ExceptionMessage}");
            };
            runner.OnExecutionComplete = info =>
            {
                _total = info.TotalTests;
                _failed = info.TestsFailed;
                _skipped = info.TestsSkipped;
                Finished.Set();
            };

            // Parallelism is disabled assembly-wide; be explicit anyway so every
            // test body is handed to the fixture serially.
            runner.Start(new AssemblyRunnerStartOptions { Parallel = false, MaxParallelThreads = 1 });
            Finished.Wait();
        }
        catch (Exception e)
        {
            _errored = true;
            Console.WriteLine($"MacRunner crashed: {e}");
        }
        finally
        {
            // Ends RunLoop on thread 0 so the process can exit.
            GtkThreadBridge.Queue.CompleteAdding();
        }
    }

    private static void Touch() => Interlocked.Exchange(ref _lastActivityTicks, Clock.ElapsedTicks);

    private static void Watchdog()
    {
        while (!Finished.IsSet)
        {
            Thread.Sleep(5000);

            // Only once tests are running: the first Gtk call may spend minutes
            // in the on-demand Homebrew install, which is not a stall.
            if (!_testsStarted)
                continue;

            var idle = (Clock.ElapsedTicks - Interlocked.Read(ref _lastActivityTicks))
                       / (double)Stopwatch.Frequency;
            if (idle > StallSeconds)
            {
                Console.Error.WriteLine(
                    $"MacRunner STALL: '{_currentTest}' ran {idle:F0}s with no progress and is " +
                    "blocking the Gtk thread; aborting. It likely opens a modal that never returns " +
                    "on headless macOS.");
                Console.Out.Flush();
                Console.Error.Flush();
                Environment.Exit(3);
            }
        }
    }

    // Non-zero when the calling thread is the process's main thread (thread 0),
    // the one Cocoa/Quartz accept for NSWindow and event handling.
    [DllImport("/usr/lib/libSystem.dylib")]
    private static extern int pthread_main_np();
}
