<#
.SYNOPSIS
  End-to-end check of the installed `gtk` workload: dotnet new, build, run headless.
.DESCRIPTION
  Creates the Gtk Application template in a scratch directory, builds it for
  net10.0-gtk4.22 and runs it for half a second. Passes when the program prints
  "SMOKE done". Uses a private template hive so templates installed globally
  (for example the GtkSharp.Template.CSharp 3.x package, which reuses the same
  template identity) cannot shadow the workload's.
.PARAMETER Sdk
  The SDK directory the workload was installed into (see Docs/workload-install.md).
.PARAMETER Packages
  Folder holding the packed GtkSharp4 packages, used as a restore source.
#>
param(
    [string]$Sdk = "$env:USERPROFILE\.dotnet-gtk",
    [string]$Packages = (Join-Path $PSScriptRoot '..\..\BuildOutput\NugetPackages'),
    [string]$Work = (Join-Path ([IO.Path]::GetTempPath()) 'gtk-workload-smoke')
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_ROOT = $Sdk
$env:DOTNET_MULTILEVEL_LOOKUP = '0'
$env:PATH = "$Sdk;$env:PATH"
$Packages = (Resolve-Path $Packages).Path

if (Test-Path $Work) { Remove-Item $Work -Recurse -Force }
New-Item -ItemType Directory $Work | Out-Null
Push-Location $Work
try {
    dotnet new gtk -n SmokeApp --debug:custom-hive (Join-Path $Work 'hive')
    if ($LASTEXITCODE) { throw 'dotnet new gtk failed' }

    Set-Location SmokeApp
    $csproj = Get-Content SmokeApp.csproj -Raw
    Set-Content SmokeApp.csproj ($csproj -replace '<TargetFramework>[^<]*</TargetFramework>', '<TargetFramework>net10.0-gtk4.22</TargetFramework>')
    Set-Content nuget.config "<configuration><packageSources><clear/><add key=`"local`" value=`"$Packages`"/></packageSources></configuration>"

    # Replace the template program with one that quits by itself.
    @'
using System;
using Gtk;

class Program
{
    [STAThread]
    static int Main()
    {
        Application.Init();
        var win = new Window();
        win.Present();
        Console.WriteLine("SMOKE window presented, gtk " + GtkVersion.Current);
        GLib.Timeout.Add(500, () => { win.Close(); Application.Quit(); return false; });
        Application.Run();
        Console.WriteLine("SMOKE done");
        return 0;
    }
}
'@ | Set-Content Program.cs

    dotnet build -o out
    if ($LASTEXITCODE) { throw 'build failed' }

    $output = & (Join-Path 'out' 'SmokeApp.exe') 2>&1 | Out-String
    Write-Host $output
    if ($output -notmatch 'SMOKE done') { throw 'the app did not run to completion' }
    Write-Host 'Workload smoke test passed.'
}
finally {
    Pop-Location
}
