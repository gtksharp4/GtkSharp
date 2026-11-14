#!/usr/bin/env bash
# CI (Linux): build GtkSharp and run the full unit-test suite on a host with NO
# system Gtk, so the bindings fetch the runtime themselves -- dogfooding
# GRuntimeInstaller -> GLinuxRuntime (conda-forge via micromamba). Runs inside a
# bare debian container, which has no system Gtk to short-circuit the fetch.
# WebKit is skipped: its sandbox needs user namespaces a container lacks.
set -euo pipefail

DOTNET_DIR=/usr/local/dotnet
export DOTNET_ROOT="$DOTNET_DIR"
export PATH="$DOTNET_DIR:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

echo "== prerequisites (deliberately NOT libgtk-4: the bindings fetch it) =="
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y -qq --no-install-recommends \
  ca-certificates curl git dbus libicu-dev xvfb xauth procps

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

echo "== clear the runtime cache so the on-demand fetch actually runs =="
rm -rf "$HOME/.local/share/GtkSharp"

echo "== run the full unit-test suite; the first Gtk call fetches the runtime =="
export GDK_BACKEND=x11
export GTKSHARP_TESTS_SKIP_WEBKIT=1
dbus-run-session -- xvfb-run -a \
  dotnet test Source/Tests/GtkSharp.Tests -c Release --no-build
