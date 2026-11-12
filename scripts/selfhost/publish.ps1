<#
.SYNOPSIS
  API'yi bu bilgisayarda canlı (Production) çalışacak şekilde derleyip D:\MatcBuddyLive\app altına koyar.

.DESCRIPTION
  - Çalışan canlı API'yi durdurur (dosyalar serbest kalsın), Release derler, app klasörüne kopyalar.
  - İlk çalıştırmada:
      * app\appsettings.Production.json üretir (JWT anahtarı yeni üretilir; Facebook secret user-secrets'tan okunur).
        Bu dosya depoya girmez ve sonraki yayınlarda korunur.
      * Mevcut fotoğrafları (src\MatcBuddy.Api\App_Data\media) data\media altına, Web Push anahtarını
        (App_Data\vapid.json) app\App_Data altına kopyalar.
      * cloudflared\config.yml üretir (api.matcbuddy.com -> http://localhost:<Port>).
  Ardından start.ps1 çalıştırılır (ya da oturum açılışındaki görev başlatır).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\selfhost\publish.ps1
#>
param(
    [string]$Root = "D:\MatcBuddyLive",
    [int]$Port = 5080,
    [string]$FrontendUrl = "https://matcbuddy.com",
    [string]$ApiHost = "api.matcbuddy.com",
    [string]$TunnelName = "matcbuddy-api"
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$apiProject = Join-Path $repo "src\MatcBuddy.Api"
$app = Join-Path $Root "app"
$data = Join-Path $Root "data"

function Step($t) { Write-Host ""; Write-Host "==> $t" -ForegroundColor Green }

# .NET 10 SDK (yalnızca kullanıcı klasöründe olabilir)
$candidates = @("$env:USERPROFILE\.dotnet", "$env:ProgramFiles\dotnet") | Where-Object { Test-Path "$_\dotnet.exe" }
$dotnetRoot = $candidates | Where-Object { Get-ChildItem "$_\sdk" -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like '10.*' } } | Select-Object -First 1
if (-not $dotnetRoot) { throw ".NET 10 SDK bulunamadı (winget install Microsoft.DotNet.SDK.10)." }
$dotnet = Join-Path $dotnetRoot "dotnet.exe"
$env:DOTNET_ROOT = $dotnetRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

New-Item -ItemType Directory -Force $Root, $app, $data, (Join-Path $Root "logs"), (Join-Path $Root "backups"), (Join-Path $Root "cloudflared") | Out-Null

Step "Çalışan canlı API durduruluyor"
Get-Process MatcBuddy.Api -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$app*" } | Stop-Process -Force
Start-Sleep -Seconds 1

Step "Derleniyor (Release)"
& $dotnet publish $apiProject -c Release -o $app --nologo -v q -clp:ErrorsOnly
if ($LASTEXITCODE -ne 0) { throw "Derleme başarısız." }

# ------------------------------------------------------------------ Canlı ayar dosyası (bir kez)
$prodFile = Join-Path $app "appsettings.Production.json"
if (-not (Test-Path $prodFile)) {
    Step "appsettings.Production.json üretiliyor (ilk kurulum)"
    $bytes = New-Object byte[] 48
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $jwtKey = [Convert]::ToBase64String($bytes)

    $dev = Get-Content (Join-Path $apiProject "appsettings.Development.json") -Raw | ConvertFrom-Json
    $fbSecret = ""
    Push-Location $apiProject
    try {
        $line = (& $dotnet user-secrets list 2>$null) | Where-Object { $_ -like "Auth:Facebook:AppSecret*" } | Select-Object -First 1
        if ($line) { $fbSecret = ($line -split "=", 2)[1].Trim() }
    } finally { Pop-Location }

    $wwwFrontend = $FrontendUrl -replace '^https://', 'https://www.'
    $prod = [ordered]@{
        Urls = "http://localhost:$Port"
        ConnectionStrings = @{ MatcBuddy = "Server=(localdb)\mssqllocaldb;Database=MatcBuddy;Trusted_Connection=true" }
        Jwt = @{ Key = $jwtKey }
        Auth = [ordered]@{
            FrontendBaseUrl = $FrontendUrl
            Google = @{ ClientId = $dev.Auth.Google.ClientId }
            Facebook = @{ AppId = $dev.Auth.Facebook.AppId; AppSecret = $fbSecret }
        }
        Cors = @{ AllowedOrigins = @($FrontendUrl, $wwwFrontend) }
        # İstek yolu: tarayıcı -> Vercel (/api) -> Cloudflare -> cloudflared (localhost) -> API
        Proxy = @{ KnownNetworks = @("0.0.0.0/0", "::/0"); ForwardLimit = 2 }
        Storage = @{ Provider = "Local"; Local = @{ Root = (Join-Path $data "media") } }
        Messaging = @{ OutboxPath = (Join-Path $data "outbox") }
        Logging = @{ File = @{ Path = (Join-Path $Root "logs\matcbuddy-.log"); RetainedFileCountLimit = 30 } }
        Swagger = @{ Enabled = $false }
    }
    [IO.File]::WriteAllText($prodFile, ($prod | ConvertTo-Json -Depth 6), (New-Object Text.UTF8Encoding($false)))
    Write-Host "Yazıldı: $prodFile (gizli değerler içerir; paylaşmayın)"
}

# ------------------------------------------------------------------ Mevcut veriler (bir kez)
$media = Join-Path $data "media"
$srcMedia = Join-Path $apiProject "App_Data\media"
if (-not (Test-Path $media) -and (Test-Path $srcMedia)) {
    Step "Mevcut fotoğraflar kopyalanıyor"
    Copy-Item $srcMedia $media -Recurse
}
$vapid = Join-Path $app "App_Data\vapid.json"
$srcVapid = Join-Path $apiProject "App_Data\vapid.json"
if (-not (Test-Path $vapid) -and (Test-Path $srcVapid)) {
    New-Item -ItemType Directory -Force (Split-Path $vapid) | Out-Null
    Copy-Item $srcVapid $vapid
}

# ------------------------------------------------------------------ Tünel ayarı (bir kez)
$cfConfig = Join-Path $Root "cloudflared\config.yml"
if (-not (Test-Path $cfConfig)) {
    Step "cloudflared ayarı üretiliyor"
    $tunnel = (& cloudflared tunnel list -o json 2>$null | ConvertFrom-Json) | Where-Object { $_.name -eq $TunnelName } | Select-Object -First 1
    if (-not $tunnel) { throw "Cloudflare tüneli bulunamadı: $TunnelName (cloudflared tunnel create $TunnelName)" }
    $cred = Join-Path $env:USERPROFILE ".cloudflared\$($tunnel.id).json"
    $yml = @"
tunnel: $($tunnel.id)
credentials-file: $cred
ingress:
  - hostname: $ApiHost
    service: http://localhost:$Port
  - service: http_status:404
"@
    [IO.File]::WriteAllText($cfConfig, $yml, (New-Object Text.UTF8Encoding($false)))
}

Write-Host ""
Write-Host "Hazır. Başlatmak için: powershell -ExecutionPolicy Bypass -File `"$PSScriptRoot\start.ps1`"" -ForegroundColor Green
