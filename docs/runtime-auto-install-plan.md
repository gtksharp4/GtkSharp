# Plan: auto-provide the Gtk 4 runtime on macOS and Linux

Extend the Windows-only auto-install so a consumer of `GtkSharp4` gets a working
Gtk 4 runtime on macOS and Linux too, without hand-installing it.

> **Status: implemented.** Linux source resolved to blocker #1 option (b):
> download the micromamba static binary and run `micromamba create -c
> conda-forge gtk4` (resolves the whole closure, nothing to host), falling back
> to the distro package manager. See `Source/Libs/Shared/GRuntimeInstaller.cs`,
> `GRuntimeProvisioning.cs`, `GLibrary.cs`, and the tests in
> `Source/Tests/GtkSharp.Tests/RuntimeProvisioningTests.cs`.
>
> **GL/display host-coupling caveat (blocker #1): verified resolved on WSLg.**
> On a clean Ubuntu 22.04 (WSL2) with no system Gtk, the exact micromamba
> commands produced `env/lib/libgtk-4.so.1` (conda-forge gtk4 4.24.1, 88-package
> closure). `ldd` showed no unresolved deps (conda's `$ORIGIN` RPATHs resolve
> the closure, bundled wayland/X11/GL included), a full-path `dlopen` (the
> GLibrary mechanism) loaded it, and `gtk_init_check` returned TRUE against the
> WSLg display. Note conda-forge ships 4.24.1, newer than the 4.22 the bindings
> target — a superset, so compatible; the cache dir name (4.22.4) is only a key.
> Still worth a native CI run on bare Debian/Fedora to cover non-WSL display
> stacks.

## Decisions (agreed)

| # | Decision |
|---|----------|
| 1 | **macOS**: if the dylib is missing, auto-run `brew install gtk4`. |
| 2 | **Linux**: try a relocatable bundle into a user-cache dir first; on failure, fall back to the distro package manager, prompting for root. |
| 3 | **Mechanism**: runtime, inside `GLibrary` (not a build-time MSBuild target). |
| 4 | **Linux loading**: full-path `dlopen` from the user-cache dir — no `LD_LIBRARY_PATH`, no profile edits. |

Consequence of (3): Windows keeps its existing **build-time** `InstallGtk` target
(`Source/Libs/GtkSharp/GtkSharp.targets:23`). macOS/Linux install happens at
**runtime** on first native call. The two platforms therefore install at
different times by design; documented, not unified.

## BLOCKING verification — do before any code

1. **There is no single-file Gtk 4 runtime for Linux.** A conda-forge `gtk4`
   package pulls its whole dependency closure (glib, gobject, gio, cairo, pango,
   pangocairo, gdk-pixbuf, graphene, harfbuzz, fribidi, epoxy, …). One tarball is
   not enough. **Choose the source and prove it loads before writing loader code:**
   - **(a) Pre-assembled hosted bundle (recommended).** Build once with
     `micromamba create -p env gtk4` on a manylinux-ish base, then tar `env/lib`
     + `env/share` into `GTK4_Linux_x64_4.22.4.tar.zst`, host it as a GitHub
     release asset (mirrors gvsbuild's Windows zip). Single download, closure
     inside. Verify `dlopen` of the bundled `libgtk-4.so.1` succeeds on a clean
     Debian and a clean Fedora container.
   - **(b) Drive micromamba at runtime.** Ship/download `micromamba`, resolve the
     closure into the user dir on first run. Heavier, needs network + solver.
   - Host/GL coupling caveat for both: the bundle must **not** override the host's
     Wayland/X11/libGL/EGL/dbus. Confirm the chosen bundle loads the host's GL
     (exclude `libEGL*`, `libGL*`, `libwayland*`, `libxkbcommon*`, `libdbus*` from
     the tarball, or it breaks against the running compositor/driver).
2. **conda-forge license/redistribution** terms for hosting a repacked bundle
   (LGPL Gtk is fine to redistribute; confirm no GPL-only component is bundled).
3. **`brew` may be absent** on macOS, and a GUI app launched without a terminal
   has nowhere to prompt for a root password on Linux — both must degrade to the
   existing `DllNotFoundException` with an actionable message, never hang.

## Affected code

- `Source/Libs/Shared/GLibrary.cs` — `TryGet` (lines 67-117) is the single hook
  point. `LoadLibrary` by full path already works on Unix
  (`FuncLoader.LoadLibrary`, `FuncLoader.cs:81-93`); macOS already falls back to
  `/usr/local/lib` and `/opt/homebrew/lib` (`GLibrary.cs:93-96`).
- `Source/Libs/Shared/FuncLoader.cs` — `IsWindows`/`IsOSX`/`IsLinux` already set
  (lines 49-79). No change expected; read-only dependency.
- New file: `Source/Libs/Shared/GRuntimeInstaller.cs` (kept < 200 lines; split by
  platform if it grows: `GRuntimeInstaller.Mac.cs`, `.Linux.cs`).
- Docs: `docs/getting-started.md`, `docs/index.md`, `README` — document the new
  behaviour and the opt-out.

## Change shape

### 1. `GLibrary.TryGet` — install-and-retry on Unix, once, core libs only

After the existing fallback loop fails (`GLibrary.cs:101-108`), before recording
the library as not-found:

```csharp
// After the fallback loop, ret may still be Zero.
if (ret == IntPtr.Zero && !FuncLoader.IsWindows && IsCoreLibrary(library)
    && GRuntimeInstaller.TryEnsureRuntime())   // runs at most once per process
{
    // Retry: macOS picks up the brew prefixes already in the fallback list;
    // Linux retries with the user-cache lib dir GRuntimeInstaller just populated.
    ret = LoadWithExtraDir(library, GRuntimeInstaller.UserLibDir);
}
```

- `IsCoreLibrary` = `{GLib, GObject, Gio, Pango, PangoCairo, Cairo, Graphene,
  GdkPixbuf, Gdk, Gsk, Gtk}`. **Excludes** `Webkit`, `JavaScriptCore`,
  `GtkSource`, `Adwaita` — those are legitimately optional and already gated by
  callers via `GLibrary.IsSupported` (`GLibrary.cs:43-49`); auto-installing for
  them would be wrong.
- `TryEnsureRuntime` guards with a static `bool _attempted` so install is tried
  **once** per process regardless of how many libraries miss.
- `LoadWithExtraDir` prepends `Path.Combine(UserLibDir, def[i])` to the existing
  per-name attempts, so Linux loads by **full path** (decision 4). No env var.

### 2. `GRuntimeInstaller` (new)

```
UserLibDir => ~/.local/share/GtkSharp/4.22.4/lib   (XDG_DATA_HOME aware)
             macOS: not used (brew prefix is the target)

TryEnsureRuntime():
  if _attempted: return _succeeded
  _attempted = true
  if IsOSX:   _succeeded = BrewInstall()              // `brew install gtk4`
  if IsLinux: _succeeded = InstallBundle()             // (a)/(b) from blocker #1
              || InstallViaPackageManager()            // fallback, root prompt
  return _succeeded
```

- `BrewInstall`: locate `brew` (`/opt/homebrew/bin/brew`, `/usr/local/bin/brew`,
  `PATH`); if absent → log actionable message, return false. Run
  `brew install gtk4` via `Process.Start`, stream output, honour the opt-out env
  var. No sudo (brew never needs it).
- `InstallBundle`: download the hosted `.tar.zst` (blocker #1a) to a temp file,
  extract into `UserLibDir`'s parent, verify `libgtk-4.so.1` present. `.tar.zst`
  extraction on netstandard2.0: shell out to `tar --zstd -xf` (present on modern
  Linux) or ship a managed zstd; decide in blocker #1.
- `InstallViaPackageManager`: detect distro (`/etc/os-release` ID), map to
  `apt-get install -y libgtk-4-1` / `dnf install -y gtk4` / `pacman -S --noconfirm
  gtk4`; run under `pkexec` (GUI root prompt) falling back to `sudo`. If neither
  present, or non-interactive → log and return false.

### 3. Opt-out

Honour an env var `GTKSHARP_SKIP_RUNTIME_INSTALL=1` (runtime analogue of
`SkipGtkInstall`). When set, `TryEnsureRuntime` is a no-op returning false, so the
existing clear `DllNotFoundException` (`GLibrary.cs:59-61`) is what the user sees.

## Verification

Unit-testable (pure, no Gtk needed — reach internals via `InternalsVisibleTo`):

| Test | Asserts |
|------|---------|
| `IsCoreLibrary` table | Webkit/JSC/GtkSource/Adwaita excluded; Gtk/GLib/… included. |
| `UserLibDir` | Honours `XDG_DATA_HOME`; falls back to `~/.local/share`. |
| distro→command map | `os-release` ID `ubuntu`/`fedora`/`arch` → correct argv. |
| opt-out | `GTKSHARP_SKIP_RUNTIME_INSTALL=1` → `TryEnsureRuntime` returns false, no process spawned (inject a fake process runner). |
| once-only guard | Second `TryEnsureRuntime` call spawns nothing. |

Extract `Process.Start` behind an injectable `IProcessRunner` so install logic is
tested without mutating the machine.

End-to-end (manual / CI containers):

- Clean **Debian forky** container, no Gtk: `dotnet run` a sample → bundle
  downloads, widget constructs. (Extends `.github/workflows/main.yml`.)
- Clean **Fedora** container: same, proving cross-distro load (blocker #1 GL
  caveat).
- **macOS** without gtk4: first run triggers `brew install gtk4`, then succeeds.
- Opt-out set on each: fails fast with the actionable `DllNotFoundException`.

## Risks

| Risk | Mitigation |
|------|-----------|
| Bundle breaks across distros / against host GL | Exclude host-owned GL/display/dbus libs from the bundle; test on ≥2 distros (blocker #1). |
| Runtime install blocks first native call for minutes (brew compile, download) | Log a clear "installing Gtk runtime, first run only" message; document; keep opt-out. |
| Root prompt in a windowless GUI app | `pkexec` for GUI; if unavailable/non-interactive, don't hang — return false + message. |
| `.tar.zst` extraction unavailable on old Linux | Prefer `.tar.gz`, or detect `tar --zstd`, or ship managed decompressor. |
| Install logic inside a P/Invoke-only library surprises embedders | Gate entirely behind opt-out; only core libs; once per process. |
| conda-forge redistribution terms | Verify licences before hosting (blocker #2). |

## Out of scope

- Moving Windows to the runtime mechanism (stays build-time; `GtkSharp.targets`).
- Auto-installing optional libs (Webkit, GtkSourceView, libadwaita) — callers
  already gate these.
