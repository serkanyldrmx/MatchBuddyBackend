<#
.SYNOPSIS
  Canlı API'yi ve Cloudflare tünelini başlatır, ayakta tutar ve her gece veritabanını yedekler.

.DESCRIPTION
  Oturum açılışındaki zamanlanmış görev (register-task.ps1) bu betiği gizli pencerede çalıştırır.
  Döngü her 30 sn'de bir:
    - API (D:\MatcBuddyLive\app\MatcBuddy.Api.exe) çalışmıyorsa başlatır,
    - cloudflared tüneli çalışmıyorsa başlatır,
    - saat 03:00'ten sonra o gün yedek alınmadıysa backup.ps1'i çalıştırır.
  Durdurmak için: stop.ps1
#>
param(
    [string]$Root = "D:\MatcBuddyLive"
)

$ErrorActionPreference = "Continue"
$app = Join-Path $Root "app"
$logs = Join-Path $Root "logs"
$cfConfig = Join-Path $Root "cloudflared\config.yml"
$exe = Join-Path $app "MatcBuddy.Api.exe"
New-Item -ItemType Directory -Force $logs | Out-Null

# .NET 10 çalışma zamanı yalnızca kullanıcı klasöründe (%USERPROFILE%\.dotnet) kurulu olabilir;
# uygulama varsayılan olarak Program Files'a baktığı için DOTNET_ROOT ile gösterilir.
$dotnetRoot = @("$env:USERPROFILE\.dotnet", "$env:ProgramFiles\dotnet") |
    Where-Object { Get-ChildItem "$_\shared\Microsoft.NETCore.App" -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like '10.*' } } |
    Select-Object -First 1

# Aynı anda iki kopya çalışmasın
$mutex = New-Object Threading.Mutex($false, "Global\MatcBuddyLiveSupervisor")
if (-not $mutex.WaitOne(0)) { Write-Host "Zaten çalışıyor."; exit 0 }

function Log($m) { Add-Content (Join-Path $logs "supervisor.log") ("{0:yyyy-MM-dd HH:mm:ss} {1}" -f (Get-Date), $m) }

function Ensure-Api {
    $p = Get-Process MatcBuddy.Api -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }
    if ($p) { return }
    if (-not (Test-Path $exe)) { Log "API bulunamadı: $exe (önce publish.ps1)"; return }
    $psi = New-Object Diagnostics.ProcessStartInfo $exe
    $psi.WorkingDirectory = $app          # içerik kökü = app (App_Data, appsettings)
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Production"
    if ($dotnetRoot) { $psi.EnvironmentVariables["DOTNET_ROOT"] = $dotnetRoot }
    [Diagnostics.Process]::Start($psi) | Out-Null
    Log "API başlatıldı"
}

function Ensure-Tunnel {
    $running = Get-CimInstance Win32_Process -Filter "Name='cloudflared.exe'" | Where-Object { $_.CommandLine -like "*$cfConfig*" }
    if ($running) { return }
    Start-Process cloudflared -ArgumentList @("tunnel", "--no-autoupdate", "--config", "`"$cfConfig`"", "--logfile", "`"$(Join-Path $logs 'cloudflared.log')`"", "run") -WindowStyle Hidden
    Log "Tünel başlatıldı"
}

function Ensure-Backup {
    $now = Get-Date
    if ($now.Hour -lt 3) { return }
    $today = Join-Path $Root ("backups\MatcBuddy_{0:yyyyMMdd}.bak" -f $now)
    if (Test-Path $today) { return }
    try {
        & (Join-Path $PSScriptRoot "backup.ps1") -Root $Root
        Log "Yedek alındı: $today"
    } catch { Log "Yedek alınamadı: $($_.Exception.Message)" }
}

Log "Denetleyici başladı"
while ($true) {
    try { Ensure-Api } catch { Log "API hatası: $($_.Exception.Message)" }
    try { Ensure-Tunnel } catch { Log "Tünel hatası: $($_.Exception.Message)" }
    Ensure-Backup
    Start-Sleep -Seconds 30
}
