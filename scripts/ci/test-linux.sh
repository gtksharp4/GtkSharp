#!/usr/bin/env bash
# CI (Linux): build GtkSharp and run the full unit-test suite on a host with NO
# system Gtk, so the bindings fetch the runtime themselves -- dogfooding
# GRuntimeInstaller -> GLinuxRuntime (conda-forge via micromamba). Runs inside a
# bare debian container, which has no system Gtk to short-circuit the fetch.
#
# Two phases: phase 1 triggers the on-demand fetch through the real C# path;
# phase 2 compiles the fetched runtime's GSettings schemas (a bare container has
# none, and libadwaita aborts without a schema source) and runs the whole suite.
# WebKit is skipped: its sandbox needs user namespaces a container lacks.
set -euo pipefail

DOTNET_DIR=/usr/local/dotnet
export DOTNET_ROOT="$DOTNET_DIR"
export PATH="$DOTNET_DIR:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

echo "== prerequisites (deliberately NOT libgtk-4: the bindings fetch it) =="
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
# fonts-dejavu-core: a bare container has no fonts at all, so Pango measures
# bold and regular identically and the attribute tests fail. at-spi2-core: the
# accessibility tests need a real GtkATContext, which the AT-SPI backend only
# provides when its bus is reachable.
apt-get install -y -qq --no-install-recommends \
  ca-certificates curl git dbus libicu-dev xvfb xauth procps \
  fonts-dejavu-core at-spi2-core

echo "== guard: the host must not already provide Gtk 4 =="
if command -v ldconfig >/dev/null 2>&1 && ldconfig -p | grep -q 'libgtk-4\.so'; then
  echo "system libgtk-4 is installed; the dogfood path needs a host WITHOUT it" >&2
  exit 2
fi

echo "== .NET 10 SDK =="
curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$DOTNET_DIR"
dotnet --version

echo "== build (generated sources + test project) =="
dotnet tool restore
dotnet cake build.cake --BuildTarget=Prepare --Configuration=Release
dotnet build Source/Tests/GtkSharp.Tests -c Release

# Headless-container GUI environment, independent of which runtime is loaded.
export GDK_BACKEND=x11            # the display is Xvfb
export GSETTINGS_BACKEND=memory  # no dconf daemon
export GTKSHARP_TESTS_SKIP_WEBKIT=1
export XDG_RUNTIME_DIR="$(mktemp -d)"

RUNTIME_DIR="$HOME/.local/share/GtkSharp"
ENVP="$RUNTIME_DIR/4.22.4/env"
# libadwaita and gtksourceview are not IsSupported-guarded in the suite (a
# sample MainWindow builds a SourceView unconditionally), so the fetch must
# include them, plus the desktop schemas libadwaita reads. WebKit tests are
# guarded and WebKit is skipped here.
export GTKSHARP_CONDA_PACKAGES="gtk4 libadwaita gtksourceview gsettings-desktop-schemas"

echo "== phase 1: dogfood the on-demand fetch through the real C# path =="
rm -rf "$RUNTIME_DIR"
dbus-run-session -- xvfb-run -a \
  dotnet test Source/Tests/GtkSharp.Tests -c Release --no-build \
    --filter 'FullyQualifiedName~NativeSymbolTests'

echo "== compile the fetched runtime's GSettings schemas and point GLib at them =="
"$ENVP/bin/glib-compile-schemas" "$ENVP/share/glib-2.0/schemas"
export GSETTINGS_SCHEMA_DIR="$ENVP/share/glib-2.0/schemas"
export XDG_DATA_DIRS="$ENVP/share:/usr/local/share:/usr/share"

echo "== phase 2: full suite, loading the fetched runtime by full path =="
dbus-run-session -- xvfb-run -a \
  dotnet test Source/Tests/GtkSharp.Tests -c Release --no-build
