#!/usr/bin/env bash
# CI (macOS): build GtkSharp and run the full unit-test suite without installing
# Gtk first, so the bindings fetch it themselves -- dogfooding GRuntimeInstaller,
# which runs `brew install gtk4` on first use. WebKit is skipped. Assumes the
# runner already has .NET 10 (actions/setup-dotnet) and Homebrew (preinstalled on
# GitHub macOS runners).
#
# No GDK_BACKEND override here: Homebrew's gtk4 builds only the Quartz backend.
# Quartz's main loop must be polled from thread 0, which the test host owns, so
# GtkFixture restores g_poll on macOS (MacQuartzMainLoop) -- see docs/testing.md.
set -euo pipefail

export DOTNET_CLI_TELEMETRY_OPTOUT=1

echo "== guard: the runner must not already provide Gtk 4 =="
# Homebrew's gtk4 would land here; a preinstalled one would skip the dogfood path.
if [ -e /opt/homebrew/lib/libgtk-4.1.dylib ] || [ -e /usr/local/lib/libgtk-4.1.dylib ]; then
  echo "Gtk 4 already present; uninstalling so the on-demand install is exercised" >&2
  brew uninstall --ignore-dependencies gtk4 2>/dev/null || true
fi

echo "== build (generated sources + test project) =="
dotnet tool restore
dotnet cake build.cake --BuildTarget=Prepare --Configuration=Release
dotnet build Source/Tests/GtkSharp.Tests -c Release

echo "== run the full unit-test suite; the first Gtk call runs 'brew install gtk4' =="
export GTKSHARP_TESTS_SKIP_WEBKIT=1
dotnet test Source/Tests/GtkSharp.Tests -c Release --no-build
