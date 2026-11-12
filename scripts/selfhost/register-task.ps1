<#
.SYNOPSIS
  Windows'a giriş yapıldığında start.ps1'i gizli pencerede başlatan zamanlanmış görevi kurar (yönetici gerekmez).
  Kaldırmak için: Unregister-ScheduledTask -TaskName "MatcBuddy Live" -Confirm:$false
#>
$start = Join-Path $PSScriptRoot "start.ps1"
$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$start`""
$trigger = New-ScheduledTaskTrigger -AtLogOn -User "$env:USERDOMAIN\$env:USERNAME"
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable `
    -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) -MultipleInstances IgnoreNew
Register-ScheduledTask -TaskName "MatcBuddy Live" -Action $action -Trigger $trigger -Settings $settings `
    -Description "MatcBuddy canlı API + Cloudflare tüneli + gece yedeği (scripts\selfhost\start.ps1)" -Force | Out-Null
Write-Host "Görev kuruldu: MatcBuddy Live (oturum açılışında başlar)."
