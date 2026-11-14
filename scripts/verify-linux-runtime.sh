#!/usr/bin/env bash
# Verifies the on-demand Gtk 4 runtime install on a Linux host with NO system
# Gtk 4. The bindings (GLibrary -> GRuntimeInstaller -> GLinuxRuntime) must fetch
# a conda-forge runtime with micromamba and load it; a Gtk-touching test is run
# so the whole C# path fires, not just the shell commands it issues.
#
# Run inside a bare distro container with the working tree mounted, e.g.:
#   docker run --rm -v "$PWD":/src -w /src debian:forky bash scripts/verify-linux-runtime.sh
#   docker run --rm -v "$PWD":/src -w /src fedora:latest bash scripts/verify-linux-runtime.sh
set -euo pipefail

DOTNET_DIR=/usr/local/dotnet
RUNTIME_CACHE="$HOME/.local/share/GtkSharp"

echo "== install prerequisites (deliberately NOT libgtk-4: the bindings fetch it) =="
if command -v apt-get >/dev/null 2>&1; then
  export DEBIAN_FRONTEND=noninteractive
  apt-get update -qq
  apt-get install -y -qq --no-install-recommends \
    ca-certificates curl git dbus libicu-dev xvfb xauth procps
elif command -v dnf >/dev/null 2>&1; then
  dnf install -y -q \
    ca-certificates curl git dbus-daemon dbus-x11 libicu \
    xorg-x11-server-Xvfb xorg-x11-xauth procps-ng
else
  echo "no supported package manager (apt-get/dnf)" >&2
  exit 2
fi

echo "== guard: the host must not already provide Gtk 4 =="
if command -v ldconfig >/dev/null 2>&1 && ldconfig -p | grep -q 'libgtk-4\.so'; then
  echo "system libgtk-4 is installed; this check needs a host WITHOUT it" >&2
  exit 2
fi

echo "== install .NET 10 SDK =="
curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$DOTNET_DIR"
export PATH="$DOTNET_DIR:$PATH"
export DOTNET_ROOT="$DOTNET_DIR"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet --version

echo "== generate sources and build the test project (no Gtk needed to build) =="
dotnet tool restore
dotnet cake build.cake --BuildTarget=Prepare --Configuration=Release
dotnet build Source/Tests/GtkSharp.Tests -c Release

echo "== clear the runtime cache so the install path actually runs =="
rm -rf "$RUNTIME_CACHE"

echo "== run a Gtk-loading test; the fixture's gtk_init triggers the fetch =="
# NativeSymbolTests inits Gtk and asserts the loaded version, so a pass means the
# runtime was fetched and loaded. x11 because the display is Xvfb.
export GDK_BACKEND=x11
dbus-run-session -- xvfb-run -a \
  dotnet test Source/Tests/GtkSharp.Tests -c Release --no-build \
    --filter 'FullyQualifiedName~NativeSymbolTests'

echo "== confirm the runtime was auto-fetched, not pre-existing =="
so=$(find "$RUNTIME_CACHE" -name 'libgtk-4.so.1' 2>/dev/null | head -n1 || true)
if [ -n "$so" ]; then
  echo "VERIFIED: the bindings fetched $(readlink -f "$so") and the Gtk tests passed"
else
  echo "FAILED: no runtime under $RUNTIME_CACHE after a passing run" >&2
  exit 1
fi
