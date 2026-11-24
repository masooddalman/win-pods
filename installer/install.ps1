# Installs WinPods for the current user and starts it with Windows. No admin rights needed.
$ErrorActionPreference = 'Stop'
$src = $PSScriptRoot
$dir = Join-Path $env:LOCALAPPDATA 'Programs\WinPods'
$exe = Join-Path $dir 'WinPods.exe'

Get-Process WinPods -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
New-Item -ItemType Directory -Force $dir | Out-Null
Copy-Item (Join-Path $src 'WinPods.exe') $dir -Force
Copy-Item (Join-Path $src 'uninstall.ps1') $dir -Force

# Start with Windows
Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'WinPods' -Value "`"$exe`""

# Start menu shortcut
$lnk = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\WinPods.lnk'
$sh = New-Object -ComObject WScript.Shell
$s = $sh.CreateShortcut($lnk); $s.TargetPath = $exe; $s.WorkingDirectory = $dir; $s.Save()

# Entry in Settings > Apps
$k = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\WinPods'
New-Item $k -Force | Out-Null
Set-ItemProperty $k DisplayName 'WinPods'
Set-ItemProperty $k DisplayIcon $exe
Set-ItemProperty $k InstallLocation $dir
Set-ItemProperty $k UninstallString "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$dir\uninstall.ps1`""
Set-ItemProperty $k NoModify 1 -Type DWord
Set-ItemProperty $k NoRepair 1 -Type DWord

Start-Process $exe
Write-Host 'WinPods is installed and will start with Windows.'
