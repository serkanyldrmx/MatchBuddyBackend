<#
.SYNOPSIS
  start.ps1'i gizli pencerede çalıştıran zamanlanmış görevi kurar (yönetici gerekmez):
    - Windows'a giriş yapıldığında başlar,
    - ayrıca her 5 dakikada bir tetiklenir: denetleyici herhangi bir sebeple kapandıysa (pencere kapatıldı,
      süreç sonlandırıldı) en geç 5 dakikada geri gelir. Zaten çalışıyorsa yeni kopya hiçbir şey yapmaz.
  Kaldırmak için: Unregister-ScheduledTask -TaskName "MatcBuddy Live" -Confirm:$false
#>
$start = Join-Path $PSScriptRoot "start.ps1"
$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$start`""
$atLogon = New-ScheduledTaskTrigger -AtLogOn -User "$env:USERDOMAIN\$env:USERNAME"
$every5 = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes 5)
$trigger = @($atLogon, $every5)
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable `
    -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) -MultipleInstances IgnoreNew
Register-ScheduledTask -TaskName "MatcBuddy Live" -Action $action -Trigger $trigger -Settings $settings `
    -Description "MatcBuddy canlı API + Cloudflare tüneli + gece yedeği (scripts\selfhost\start.ps1)" -Force | Out-Null
Write-Host "Görev kuruldu: MatcBuddy Live (oturum açılışında ve her 5 dakikada bir kontrol eder)."
