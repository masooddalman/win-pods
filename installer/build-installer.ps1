# Builds dist\WinPods-Setup: WinPods.exe + Install.cmd. Zip that folder to share it.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'dist\WinPods-Setup'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $root 'WinPods.csproj') -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $out
if ($LASTEXITCODE -ne 0) { throw 'publish failed' }
Remove-Item (Join-Path $out '*.pdb') -ErrorAction SilentlyContinue
Copy-Item (Join-Path $PSScriptRoot 'install.ps1'), (Join-Path $PSScriptRoot 'uninstall.ps1'), (Join-Path $PSScriptRoot 'Install.cmd') $out
Write-Host "Done: $out"
