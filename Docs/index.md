# Documentation index

## Guides (`Docs/`)

| Document | Topic |
|---|---|
| [getting-started.md](getting-started.md) | Writing Gtk 4 desktop apps in C# with GtkSharp |
| [migrating-to-gtk4.md](migrating-to-gtk4.md) | Porting apps from gtk-sharp 2.12 / GtkSharp 3 |
| [architecture.md](architecture.md) | How the repository is put together, and why |
| [testing.md](testing.md) | The xunit test suite and how to run it |
| [coverage.md](coverage.md) | Test coverage of the hand-written code |
| [workload-install.md](workload-install.md) | Installing the `gtk` workload into a private SDK |
| [article-building-gtk4-desktop-apps-in-csharp.md](article-building-gtk4-desktop-apps-in-csharp.md) | Article: building Gtk 4 desktop apps in C# |

## Repository

| Document | Topic |
|---|---|
| [README.md](../README.md) | Project overview, branches, packages, build |
| [CLAUDE.md](../CLAUDE.md) | Build, codegen pipeline and conventions for Claude Code |
| [SampleApps/README.md](../SampleApps/README.md) | Standalone samples consuming GtkSharp as a NuGet package |
| [Source/Samples/README.md](../Source/Samples/README.md) | In-repo samples app |
| [Source/Gir/README.md](../Source/Gir/README.md) | Vendored `.gir` files: provenance and sha256 |

## Bindings (`Source/Libs/`)

| Package | Wraps |
|---|---|
| [GLibSharp4](../Source/Libs/GLibSharp/README.md) | GLib / GObject |
| [GioSharp4](../Source/Libs/GioSharp/README.md) | Gio |
| [CairoSharp4](../Source/Libs/CairoSharp/README.md) | Cairo |
| [PangoSharp4](../Source/Libs/PangoSharp/README.md) | Pango |
| [GrapheneSharp4](../Source/Libs/GrapheneSharp/README.md) | Graphene |
| [GdkSharp4](../Source/Libs/GdkSharp/README.md) | Gdk |
| [GskSharp4](../Source/Libs/GskSharp/README.md) | Gsk |
| [GtkSharp4](../Source/Libs/GtkSharp/README.md) | Gtk 4 |
| [AdwaitaSharp4](../Source/Libs/AdwaitaSharp/README.md) | libadwaita |
| [GtkSourceSharp4](../Source/Libs/GtkSourceSharp/README.md) | GtkSourceView |
| [WebkitGtkSharp4](../Source/Libs/WebkitGtkSharp/README.md) | WebKitGTK 6.0 |
| [JavaScriptCoreSharp4](../Source/Libs/JavaScriptCoreSharp/README.md) | JavaScriptCore |

## Templates (`Source/Templates/`)

- [GtkSharp4.Template.CSharp](../Source/Templates/GtkSharp.Template.CSharp/README.md)
- [GtkSharp4.Template.FSharp](../Source/Templates/GtkSharp.Template.FSharp/README.md)
- [GtkSharp4.Template.VBNet](../Source/Templates/GtkSharp.Template.VBNet/README.md)

## Workload (`Source/Workload/`)

| Package | Role |
|---|---|
| [GtkSharp4.NET.Sdk.Gtk](../Source/Workload/GtkSharp.NET.Sdk.Gtk/README.md) | Workload manifest |
| [GtkSharp4.Sdk](../Source/Workload/GtkSharp.Sdk/README.md) | SDK pack |
| [GtkSharp4.Ref](../Source/Workload/GtkSharp.Ref/README.md) | Targeting pack |
| [GtkSharp4.Runtime](../Source/Workload/GtkSharp.Runtime/README.md) | Runtime pack |
| [GtkSharp4.Workload.Template.CSharp](../Source/Workload/GtkSharp.Workload.Template.CSharp/README.md) | C# templates |
| [GtkSharp4.Workload.Template.FSharp](../Source/Workload/GtkSharp.Workload.Template.FSharp/README.md) | F# templates |
| [GtkSharp4.Workload.Template.VBNet](../Source/Workload/GtkSharp.Workload.Template.VBNet/README.md) | VB templates |

## Legacy (not built)

- [Source/OldStuff/sources/Generating-Sources.md](../Source/OldStuff/sources/Generating-Sources.md) — mono-era source generation notes
