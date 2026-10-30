# Installing the `gtk` workload (private SDK recipe)

Proves `net10.0-gtk4.22` end to end (gates 10 and 11 of `gtk4-upgrade-plan.md`) without touching the
machine-wide SDK.

## Recipe (Windows, PowerShell)

| Step | Command |
|:--|:--|
| 1. Private SDK | `dotnet-install.ps1 -Channel 10.0 -InstallDir $env:USERPROFILE\.dotnet-gtk` |
| 2. Build + pack (default SDK) | `dotnet cake build.cake --BuildTarget=Default` |
| 3. Install into the private SDK | `$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet-gtk"; dotnet cake build.cake --BuildTarget=InstallPackedWorkload` |
| 4. Check | `$env:DOTNET_ROOT=...; $env:PATH="$env:USERPROFILE\.dotnet-gtk;$env:PATH"; dotnet workload list` shows `gtk` |
| 5. Smoke test | `pwsh Source/Workload/Test-WorkloadSmoke.ps1` (dotnet new gtk, build `net10.0-gtk4.22`, run, expects `SMOKE done`) |

Why two cake runs: `DOTNET_ROOT` also steers apphosts such as `NuGetUtility.exe`, which need a .NET 8 runtime a
fresh SDK does not carry, so the build runs under the default SDK and only the install sees the private one.
`InstallWorkload` (build + pack + install in one) still exists for a machine-wide SDK.

The install is plain file copies (manifest to `sdk-manifests/<band>`, packs to `packs/`, templates to
`template-packs/`, marker to `metadata/workloads/<band>/InstalledWorkloads`). The registry entry is written only
when an MSI install marker exists, so a private SDK never touches HKLM. The band is the SDK's (10.0.400 for
10.0.403); manifests are packed for 10.0.100 to 10.0.400.

## Using it

```
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet-gtk"
$env:DOTNET_MULTILEVEL_LOOKUP = "0"
$env:PATH = "$env:USERPROFILE\.dotnet-gtk;$env:PATH"
dotnet new gtk -n App --debug:custom-hive <dir>   # hive: see below
```

Project: `<TargetFramework>net10.0-gtk4.22</TargetFramework>` (the template default `net10.0-gtk` also works).
`SelfContained` and `RuntimeIdentifier` default to true / the host RID only when the project does not set them;
`-p:SelfContained=false` is honoured (GtkSharp is then not copied: framework-dependent builds cannot see the
workload runtime pack).

## Gotchas

| Symptom | Cause / fix |
|:--|:--|
| `dotnet new gtk` produces a net6.0 + `GtkSharp 3.24` project | The globally installed `GtkSharp.Template.CSharp` 3.x package has the same template identity and wins. Use `--debug:custom-hive <empty dir>` or `dotnet new uninstall GtkSharp.Template.CSharp` |
| `NuGetUtility.exe` "framework 8.0.0 not found" | `DOTNET_ROOT` was set during the build; set it only for step 3 |
| Restore: "No NuGet sources are defined" | Add a `nuget.config` with `BuildOutput/NugetPackages` (and nuget.org) |
| Tests / restore pick a stale `GtkSharp4` | Same version string is cached in `~/.nuget/packages`; restore with `--packages <dir>` |
| `dotnet test` of `GtkSharp.Tests` hangs with no output | The coverlet data collector (from `coverlet.runsettings`) stalls on a .NET 11 preview host; pass `--settings <empty .runsettings>` |

## Defects fixed on the way (G1, G2)

| Where | Fix |
|:--|:--|
| `GtkSharp.csproj` | Pack `GtkSharp.targets` as `build/` and `buildTransitive/` `$(PackageId).targets` (NuGet imports only that name); the Windows Gtk download now runs for PackageReference consumers |
| `Workload/Shared/Frameworks.targets` | `TargetFrameworkVersion` from `_GtkSharpNetVersion` (was 8.0) |
| `Workload/Directory.Build.props` | Default manifest band `$(_GtkSharpNetVersion).100` (was 8.0.200) |
| `Sdk.in.targets` | `SelfContained` defaults to true only when unset |
| `Shared/Templates.targets` | Template pack ids were `...Template.CSharp4` (blanket `Sharp`->`Sharp4`); now `GtkSharp4.Workload.Template.CSharp` as the install expects |
| `Application` template | `EventArgs` was ambiguous with `Gtk.EventArgs`; now `System.EventArgs` |
| `build.cake` | `InstallPackedWorkload` split out of `InstallWorkload` |

Not done: the same recipe against a Linux SDK (WSL); the packs only declare `win-x64;win-x86;linux-x64;linux-x86;osx-x64` runtime identifiers (no arm64).
