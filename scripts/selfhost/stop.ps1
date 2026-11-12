<#
.SYNOPSIS
  Canlı API'yi, tüneli ve denetleyiciyi durdurur (güncelleme / bakım için).
#>
param([string]$Root = "D:\MatcBuddyLive")

Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" |
    Where-Object { $_.CommandLine -like "*selfhost\start.ps1*" } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
Get-Process MatcBuddy.Api -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$Root*" } | Stop-Process -Force
Get-CimInstance Win32_Process -Filter "Name='cloudflared.exe'" |
    Where-Object { $_.CommandLine -like "*$Root*" } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
Write-Host "Durduruldu."
