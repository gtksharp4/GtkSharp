# CI (Windows): build GtkSharp and run the full unit-test suite. The Windows Gtk
# 4 runtime comes from the binding's own GtkSharp.targets (a gvsbuild download),
# which auto-imports only for package consumers -- the test project is a
# ProjectReference, so its InstallGtk target is invoked directly here. WebKit is
# not available on Windows and is skipped. Assumes .NET 10 (actions/setup-dotnet).
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

Write-Host '== build (generated sources + test project) =='
dotnet tool restore
if ($LASTEXITCODE) { exit $LASTEXITCODE }
dotnet cake build.cake --BuildTarget=Prepare --Configuration=Release
if ($LASTEXITCODE) { exit $LASTEXITCODE }
dotnet build Source/Tests/GtkSharp.Tests -c Release
if ($LASTEXITCODE) { exit $LASTEXITCODE }

Write-Host "== fetch the Gtk 4 runtime via the binding's own InstallGtk target =="
dotnet msbuild Source/Libs/GtkSharp/GtkSharp.targets -t:InstallGtk
if ($LASTEXITCODE) { exit $LASTEXITCODE }

Write-Host '== run the full unit-test suite (WebKit not available on Windows) =='
$env:GTKSHARP_TESTS_SKIP_WEBKIT = '1'
dotnet test Source/Tests/GtkSharp.Tests -c Release --no-build
exit $LASTEXITCODE
