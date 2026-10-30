using System;
using System.Linq.Expressions;
using System.Runtime.InteropServices;

class FuncLoader
{
    private class Windows
    {
        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr LoadLibraryW(string lpszLib);
    }

    private class Linux
    {
        [DllImport("libdl.so.2")]
        public static extern IntPtr dlopen(string path, int flags);

        [DllImport("libdl.so.2")]
        public static extern IntPtr dlsym(IntPtr handle, string symbol);
    }

    private class OSX
    {
        [DllImport("/usr/lib/libSystem.dylib")]
        public static extern IntPtr dlopen(string path, int flags);

        [DllImport("/usr/lib/libSystem.dylib")]
        public static extern IntPtr dlsym(IntPtr handle, string symbol);
    }

    private class Unix
    {
        [DllImport("libc")]
        public static extern IntPtr dlopen(string path, int flags);

        [DllImport("libc")]
        public static extern IntPtr dlsym(IntPtr handle, string symbol);
    }

    [DllImport("libc")]
    private static extern int uname(IntPtr buf);

    private const int RTLD_LAZY = 0x0001;
    private const int RTLD_GLOBAL = 0x0100;

    public static bool IsWindows, IsOSX, IsLinux;

    static FuncLoader()
    {
        switch (Environment.OSVersion.Platform)
        {
            case PlatformID.Win32NT:
            case PlatformID.Win32S:
            case PlatformID.Win32Windows:
            case PlatformID.WinCE:
                IsWindows = true;
                break;
            case PlatformID.MacOSX:
                IsOSX = true;
                break;
            case PlatformID.Unix:
                try
                {
                    var buf = Marshal.AllocHGlobal(8192);
                    if (uname(buf) == 0 && Marshal.PtrToStringAnsi(buf) == "Darwin")
                        IsOSX = true;
                    if (uname(buf) == 0 && Marshal.PtrToStringAnsi(buf) == "Linux")
                        IsLinux = true;

                    Marshal.FreeHGlobal(buf);
                }
                catch { }

                break;
        }
    }

    public static IntPtr LoadLibrary(string libname)
    {
        if (IsWindows)
            return Windows.LoadLibraryW(libname);

        if (IsOSX)
            return OSX.dlopen(libname, RTLD_GLOBAL | RTLD_LAZY);

        if (IsLinux)
            return Linux.dlopen(libname, RTLD_GLOBAL | RTLD_LAZY);

        return Unix.dlopen(libname, RTLD_GLOBAL | RTLD_LAZY);
    }

    // The name of the last symbol GetProcAddress failed to resolve on this
    // thread. Generated code is written LoadFunction<T>(GetProcAddress(lib, name)),
    // so the name is out of LoadFunction's sight; this hands it across without
    // changing a single generated call site.
    [ThreadStatic]
    private static string _lastMissingSymbol;

    public static IntPtr GetProcAddress(IntPtr library, string function)
    {
        var ret = IntPtr.Zero;

        if (IsWindows)
            ret = Windows.GetProcAddress(library, function);
        else if (IsOSX)
            ret = OSX.dlsym(library, function);
        else if (IsLinux)
            ret = Linux.dlsym(library, function);
        else
            ret = Unix.dlsym(library, function);

        _lastMissingSymbol = ret == IntPtr.Zero ? function : null;

        return ret;
    }

    /// <summary>Whether <paramref name="symbol"/> is exported by <paramref name="library"/>.</summary>
    public static bool IsAvailable(Library library, string symbol)
    {
        if (!GLibrary.IsSupported(library))
            return false;

        var found = GetProcAddress(GLibrary.Load(library), symbol) != IntPtr.Zero;
        _lastMissingSymbol = null;

        return found;
    }

    /// <summary>
    /// Binds a native function pointer to a delegate.
    /// </summary>
    /// <remarks>
    /// A symbol the loaded library does not export (a binding generated from Gtk
    /// 4.22 running against 4.14, say) used to come back as a null delegate, so
    /// the call compiled and then died with a NullReferenceException that named
    /// nothing. It now comes back as a delegate of the same type that throws a
    /// NotSupportedException naming the symbol, so the failure is attributable
    /// and is raised where the call is made, not where the type initialises.
    /// </remarks>
    public static T LoadFunction<T>(IntPtr procaddress)
    {
        if (procaddress == IntPtr.Zero)
        {
            var name = _lastMissingSymbol;
            _lastMissingSymbol = null;

            return CreateMissingSymbolThrower<T>(name);
        }

        return Marshal.GetDelegateForFunctionPointer<T>(procaddress);
    }

    private static T CreateMissingSymbolThrower<T>(string symbol)
    {
        try
        {
            var invoke = typeof(T).GetMethod("Invoke");
            if (invoke == null)
                return default(T);

            var parameters = invoke.GetParameters();
            var arguments = new ParameterExpression[parameters.Length];
            for (int i = 0; i < arguments.Length; i++)
                arguments[i] = Expression.Parameter(parameters[i].ParameterType, parameters[i].Name);

            var message = "The native function '" + (symbol ?? typeof(T).Name) + "' is not exported by the loaded library. "
                + "The bindings are generated against Gtk 4.22, and the installed version is older than the release that added it; "
                + "guard the call with Gtk.Global.CheckVersion or GLib.Global.IsSymbolAvailable.";

            // System.Linq.Expressions rather than Reflection.Emit: it is in
            // netstandard2.0 and builds a delegate of any signature, ref
            // parameters included.
            var body = Expression.Throw(
                Expression.New(typeof(NotSupportedException).GetConstructor(new[] { typeof(string) }), Expression.Constant(message)),
                invoke.ReturnType);

            return Expression.Lambda<T>(body, arguments).Compile();
        }
        catch
        {
            return default(T);
        }
    }
}
