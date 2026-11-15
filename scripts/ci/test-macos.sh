#!/usr/bin/env bash
# CI (macOS): build GtkSharp and run the full unit-test suite, letting the
# bindings fetch the runtime themselves -- dogfooding GRuntimeInstaller, which
# runs `brew install` on first use.
#
# The only GDK backend Homebrew's gtk4 builds is Quartz, which requires Gtk --
# NSWindow instantiation and main-loop polling -- to run on thread 0. The dotnet
# test host owns thread 0, so macOS runs the suite through GtkSharp.Tests.MacRunner
# instead: it owns thread 0 itself and marshals every test body onto it. Windows
# and Linux keep using dotnet test.
#
# The suite builds libadwaita and GtkSourceView widgets unconditionally, so the
# on-demand install must fetch them too: GTKSHARP_BREW_PACKAGES adds them to the
# `brew install` the way GTKSHARP_CONDA_PACKAGES does on Linux. WebKit is skipped.
# Assumes the runner already has .NET 10 (actions/setup-dotnet) and Homebrew
# (preinstalled on GitHub macOS runners).
set -euo pipefail

export DOTNET_CLI_TELEMETRY_OPTOUT=1

echo "== guard: the runner must not already provide Gtk 4 =="
# Remove gtk4 and the companions that depend on it, so the on-demand install is
# exercised rather than short-circuited.
for formula in gtksourceview5 libadwaita gtk4; do
  brew uninstall --ignore-dependencies "$formula" 2>/dev/null || true
done

echo "== build the macOS test runner (generated sources + runner + test project) =="
dotnet tool restore
dotnet cake build.cake --BuildTarget=Prepare --Configuration=Release
dotnet build Source/Tests/GtkSharp.Tests.MacRunner -c Release

echo "== run the full suite on thread 0; the first Gtk call dogfoods the Homebrew install =="
export GTKSHARP_TESTS_SKIP_WEBKIT=1
export GTKSHARP_BREW_PACKAGES="gtk4 gtksourceview5 libadwaita gsettings-desktop-schemas"
dotnet exec BuildOutput/Tests/Release/MacRunner/GtkSharp.Tests.MacRunner.dll
