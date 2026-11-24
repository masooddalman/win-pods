$ErrorActionPreference = 'SilentlyContinue'
$dir = $PSScriptRoot
Get-Process WinPods | Stop-Process -Force
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'WinPods'
Remove-Item 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\WinPods' -Recurse
Remove-Item (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\WinPods.lnk')
# This script lives in the folder, so delete the folder after it exits.
Start-Process cmd.exe -ArgumentList "/c timeout /t 2 /nobreak >nul & rmdir /s /q `"$dir`"" -WindowStyle Hidden
Write-Host 'WinPods is removed.'
