# GtkSharp

GtkSharp is a C# wrapper for Gtk and its related components. The component list includes the following libraries: glib, gio, cairo, pango, graphene, gdk, gsk. This is a fork of https://github.com/mono/gtk-sharp and is maintained completely separately from that project.

## Branches

| Branch    | Gtk version | Package version line | Status                                                     |
|:----------|:------------|:---------------------|:-----------------------------------------------------------|
| `develop` | Gtk 3.24    | `3.24.24.x`          | Gtk 3 maintenance line.                                     |
| `gtk4`    | Gtk 4.22.4  | `4.22.4.x`           | Gtk 4 upgrade, in progress — see [Docs/gtk4-upgrade-plan.md](Docs/gtk4-upgrade-plan.md). |

The Gtk 4 line is a hard break: same assembly names and namespaces, no side-by-side install with the 3.24.x packages.

Differences can be seen with the following table:

|               | Target framework   | Target Gtk Version                                 | Extra notes                   |
|:--------------|:-------------------|:---------------------------------------------------|:------------------------------|
| GtkSharp      | .NET Standard 2.0  | Gtk 4.22 (`gtk4` branch), Gtk 3.22 (`develop`)      | Does not need glue libraries. |
| mono/gtksharp | .NET Framework 4.5 | Gtk 2 (also Gtk 3.0 but never officially released) |                               |

* [Building from source](#building-from-source)
* [Using the library](#using-the-library)
* [License](#license)

## Building from source

Pre requirements for building from source are that you have .NET 10 installed on the system.

To build the repository, simply do:

```sh
git clone https://github.com/pieroviano/GtkSharp.git
cd GtkSharp
dotnet tool restore
dotnet cake build.cake
```

A breakdown on how the source is structured:

* Tools that are needed to generate wrapper code are found in [Tools](Source/Tools) folder
* The actual wrappers code is found in [Libs](Source/Libs) folder
* Templates are located in [Templates](Source/Templates) folder
* Build script is separated between [build.cake](build.cake) and [CakeScripts](CakeScripts) folder

## Using the library

On macOS, you'll need to manually install Gtk, see [Installing Gtk on Mac](https://github.com/pieroviano/GtkSharp/wiki/Installing-Gtk-on-Mac) wiki page for more details on how to do it.

### NuGet packages

`dotnet cake build.cake` writes every package to `BuildOutput/NugetPackages`, versioned `4.22.4.x`. Package ids carry a `4` suffix (`GtkSharp4`) so they never collide with the Gtk 3 `3.24.24.x` packages; assembly names and namespaces are unchanged (`GtkSharp.dll`, `namespace Gtk`).

**Bindings** (`PackageNuGet`, `netstandard2.0`, with `.snupkg` symbols):

| Package               | Binds                        | Depends on                          |
|:----------------------|:-----------------------------|:------------------------------------|
| `GLibSharp4`          | glib, gobject                | —                                   |
| `GioSharp4`           | gio                          | GLib                                |
| `CairoSharp4`         | cairo                        | —                                   |
| `GrapheneSharp4`      | graphene                     | GLib                                |
| `PangoSharp4`         | pango, pangocairo            | GLib, Cairo                         |
| `GdkSharp4`           | gdk, gdk-pixbuf              | GLib, Gio, Cairo, Pango, Graphene   |
| `GskSharp4`           | gsk                          | Gdk                                 |
| `GtkSharp4`           | gtk                          | Gsk; on Windows its build targets download the Gtk 4 runtime (opt out: `SkipGtkInstall=True`) |
| `AdwaitaSharp4`       | libadwaita                   | Gtk                                 |
| `GtkSourceSharp4`     | gtksourceview                | Gtk                                 |
| `JavaScriptCoreSharp4`| javascriptcore               | GLib, Gio                           |
| `WebkitGtkSharp4`     | webkitgtk                    | Gtk, JavaScriptCore                 |

`AtkSharp` is gone: Gtk 4 dropped ATK in favour of the accessibility interfaces built into Gtk itself. `GskSharp4`, `GrapheneSharp4`, `AdwaitaSharp4` and `JavaScriptCoreSharp4` are new in the 4.x line. WebKit has no Windows runtime (gvsbuild ships none), so `WebkitGtkSharp4`/`JavaScriptCoreSharp4` are Linux/macOS in practice.

**Project templates** (`PackageTemplates`, standalone `dotnet new`):

| Package                       | Language |
|:------------------------------|:---------|
| `GtkSharp4.Template.CSharp`   | C#       |
| `GtkSharp4.Template.FSharp`   | F#       |
| `GtkSharp4.Template.VBNet`    | VB.NET   |

```sh
dotnet new install GtkSharp4.Template.CSharp     # uninstall: dotnet new uninstall GtkSharp4.Template.CSharp
dotnet new gtkapp
```

**.NET workload** (`PackageWorkload`, the `gtk` workload and its `net10.0-gtk4.22` TFM):

| Package                                        | Role                                                    |
|:-----------------------------------------------|:--------------------------------------------------------|
| `GtkSharp4.NET.Sdk.Gtk.Manifest-<band>`        | Workload manifest, one per SDK band (`10.0.100`…`10.0.400`) |
| `GtkSharp4.Sdk`                                | SDK pack enabling the `-gtk` TFM                        |
| `GtkSharp4.Ref`                                | Targeting pack (reference assemblies)                   |
| `GtkSharp4.Runtime`                            | Runtime pack (implementation assemblies)                |
| `GtkSharp4.Workload.Template.CSharp`/`FSharp`/`VBNet` | Templates installed with the workload            |

Install from the local packages: `dotnet cake build.cake --BuildTarget=InstallPackedWorkload` — see [Docs/workload-install.md](Docs/workload-install.md).

## License

GtkSharp and its related components are licensed under [LGPL v2.0 license](LICENSE), while [Samples](Source/Samples) are licenced under [The Unlicense](Source/Samples/LICENSE).
