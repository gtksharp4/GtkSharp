# SampleApps

Standalone sample applications that consume GtkSharp **as a NuGet package**,
the way an application author would. They are deliberately outside
`Source/GtkSharp.sln` and use `PackageReference` rather than `ProjectReference`,
so they exercise the packaging as well as the binding.

That distinction earns its keep: the first build of `GettingStarted` failed with
`MSB4024` because `Source/Libs/GtkSharp/GtkSharp.targets` — which ships inside
the package and is imported by the **consumer's** build — contained a double
hyphen inside an XML comment. Nothing in this repository imports that file, so
no amount of building `GtkSharp.sln` could have caught it.

| Sample | What it covers |
|:--|:--|
| [GettingStarted](GettingStarted/) | One page per section of [`docs/getting-started.md`](../docs/getting-started.md), demonstrated by running rather than described. |

## Building

The packages must be resolvable. [`NuGet.config`](NuGet.config) lists
`../BuildOutput/NugetPackages` before nuget.org, so packages built in this
repository are picked up first:

```sh
# from the repository root, once
dotnet cake build.cake --BuildTarget=PackageNuGet --Configuration=Release

# then
cd SampleApps
dotnet build GettingStarted.slnx
dotnet run --project GettingStarted
```

You also need a **Gtk 4 runtime**. On Windows the package's `GtkSharp.targets`
downloads a gvsbuild bundle into `%LOCALAPPDATA%\Gtk\4.22.4` on first build
(`-p:SkipGtkInstall=True` opts out); elsewhere install `libgtk-4-1` / `gtk4`
from your distribution.

## Package ids

The projects reference the `4`-suffixed ids (`GtkSharp4`, `GLibSharp4`, …) at
`$(GtkSharpVersion)` (`4.22.4.*`). **Only the id carries the suffix** — the
assemblies inside are `GtkSharp.dll`, `GLibSharp.dll` and so on, and the
namespaces are unprefixed.

| Property | Default | Effect |
|:--|:--|:--|
| `GtkSharpVersion` | `4.22.4.*` | Package version to restore |
| `GtkSharpPackagePrefix` | empty | Prepended to every id, for a feed that publishes them under a prefix |
