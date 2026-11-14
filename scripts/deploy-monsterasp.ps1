<#
  MatcBuddy API'yi MonsterASP.NET'e (ücretsiz plan) yükler — docs/deploy-monsterasp.md.

  Ne yapar:
    1. API'yi Release olarak derler (dotnet publish).
    2. Canlı ayar dosyasını (appsettings.Production.json) üretir: veritabanı, JWT anahtarı, Google/Facebook anahtarları.
    3. (İlk kurulumda) yerel veritabanını demo verileriyle birlikte sunucudaki boş veritabanına taşır (SqlPackage).
    4. Dosyaları FTP ile yükler (yüklerken site kısa süre "bakımda" sayfası gösterir).
    5. https://<site>/api/Auth/Providers ile çalıştığını kontrol eder.

  Şifreler yalnızca bu pencerede sorulur; hiçbir dosyaya ya da depoya yazılmaz.
  JWT anahtarı ilk çalıştırmada üretilir ve %APPDATA%\MatcBuddyDeploy\jwt.key içinde saklanır (sonraki yüklemelerde oturumlar düşmesin).

  Kullanım (backend deposunun kökünde, PowerShell):
    powershell -ExecutionPolicy Bypass -File scripts\deploy-monsterasp.ps1              # ilk kurulum: veritabanı + uygulama
    powershell -ExecutionPolicy Bypass -File scripts\deploy-monsterasp.ps1 -SkipDatabase # sonraki güncellemeler: yalnızca uygulama
#>
param(
  [string]$SiteUrl = "https://matcbuddy.runasp.net",
  # Canlı ön yüz adresi: e-posta doğrulama / davet linkleri ve CORS için kullanılır
  [string]$FrontendUrl = "https://matcbuddy.com",
  [string]$FtpHost = "",
  [string]$FtpUser = "",
  [string]$FtpRoot = "/wwwroot",
  [switch]$SkipDatabase,
  # Hazır .bacpac dosyası (yerel veritabanı bu oturumda başlatılamıyorsa önceden dışa aktarılmış dosya)
  [string]$BacpacPath = "",
  [switch]$SkipUpload
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$repo = Split-Path -Parent $PSScriptRoot
$api = Join-Path $repo "src\MatcBuddy.Api"
$work = Join-Path $env:TEMP "matcbuddy-deploy"
$out = Join-Path $work "publish"
$env:PATH = "$env:USERPROFILE\.dotnet;$env:USERPROFILE\.dotnet\tools;$env:PATH"
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

function Step($t) { Write-Host ""; Write-Host "==> $t" -ForegroundColor Green }
function Plain([Security.SecureString]$s) { [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)) }

# ------------------------------------------------------------------ Bilgiler
Step "Bilgiler (MonsterASP panelinden kopyala)"
$conn = Plain (Read-Host "Veritabanı bağlantı dizesi (Databases > bağlantı bilgisi; şifre dahil)" -AsSecureString)
if ([string]::IsNullOrWhiteSpace($conn)) { throw "Bağlantı dizesi boş olamaz." }
if ($conn -notmatch "TrustServerCertificate") { $conn = $conn.TrimEnd(';') + ";TrustServerCertificate=True" }
if (-not $SkipUpload) {
  Write-Host "FTP bilgileri VERİTABANI sayfasında değil, WEB SİTESİ (matcbuddy.runasp.net) sayfasındaki FTP / Deploy bölümünde." -ForegroundColor Yellow
  while ($true) {
    if (-not $FtpHost) { $FtpHost = Read-Host "FTP sunucusu (ör. site12345.siteasp.net)" }
    $FtpHost = $FtpHost.Trim() -replace '^(ftps?|https?)://', '' -replace '[:/].*$', ''
    if ($FtpHost -match 'databaseasp\.net$') {
      Write-Host "Bu veritabanı sunucusu, FTP sunucusu değil. Web sitesinin FTP bilgisindeki adresi gir." -ForegroundColor Red
      $FtpHost = ""; continue
    }
    break
  }
  if (-not $FtpUser) { $FtpUser = Read-Host "FTP kullanıcı adı" }
  $ftpPass = Plain (Read-Host "FTP şifresi" -AsSecureString)
}

# ------------------------------------------------------------------ Anahtarlar
$secretDir = Join-Path $env:APPDATA "MatcBuddyDeploy"
New-Item -ItemType Directory -Force $secretDir | Out-Null
$jwtFile = Join-Path $secretDir "jwt.key"
if (-not (Test-Path $jwtFile)) {
  $bytes = New-Object byte[] 48; [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
  [IO.File]::WriteAllText($jwtFile, [Convert]::ToBase64String($bytes))
}
$jwtKey = (Get-Content $jwtFile -Raw).Trim()

$dev = Get-Content (Join-Path $api "appsettings.Development.json") -Raw | ConvertFrom-Json
$googleId = $dev.Auth.Google.ClientId
$fbId = $dev.Auth.Facebook.AppId
$fbSecret = ""
Push-Location $api
try {
  $line = (& dotnet user-secrets list 2>$null) | Where-Object { $_ -like "Auth:Facebook:AppSecret*" } | Select-Object -First 1
  if ($line) { $fbSecret = ($line -split "=", 2)[1].Trim() }
} finally { Pop-Location }

# ------------------------------------------------------------------ Derleme
Step "API derleniyor"
if (Test-Path $work) { Remove-Item -Recurse -Force $work }
& dotnet publish $api -c Release -o $out --nologo -v q -clp:ErrorsOnly
if ($LASTEXITCODE -ne 0) { throw "Derleme başarısız." }

$prod = [ordered]@{
  ConnectionStrings = @{ MatcBuddy = $conn }
  Jwt = @{ Key = $jwtKey }
  Auth = @{
    FrontendBaseUrl = $FrontendUrl
    Google = @{ ClientId = $googleId }
    Facebook = @{ AppId = $fbId; AppSecret = $fbSecret }
  }
  # Ön yüz (Vercel, $FrontendUrl) /api isteklerini buraya iletir; gerçek istemci IP'si X-Forwarded-For'dan okunur
  Proxy = @{ KnownNetworks = @("0.0.0.0/0", "::/0"); ForwardLimit = 1 }
  Cors = @{ AllowedOrigins = @($FrontendUrl, ($FrontendUrl -replace '^https://', 'https://www.'), "http://localhost:3000") }
  Swagger = @{ Enabled = $false }
}
$prodJson = $prod | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText((Join-Path $out "appsettings.Production.json"), $prodJson, (New-Object Text.UTF8Encoding($false)))

# Yüklenmiş fotoğraflar ve bildirim (VAPID) anahtarı
$appData = Join-Path $api "App_Data"
foreach ($item in @("media", "vapid.json")) {
  $src = Join-Path $appData $item
  if (Test-Path $src) {
    New-Item -ItemType Directory -Force (Join-Path $out "App_Data") | Out-Null
    Copy-Item $src (Join-Path $out "App_Data") -Recurse -Force
  }
}

# ------------------------------------------------------------------ Veritabanı
if (-not $SkipDatabase) {
  Step "Veritabanı taşınıyor (yerel demo verileri -> sunucu)"
  if ($BacpacPath) {
    if (-not (Test-Path $BacpacPath)) { throw "Bacpac dosyası bulunamadı: $BacpacPath" }
    $bacpac = $BacpacPath
  } else {
    $bacpac = Join-Path $work "MatcBuddy.bacpac"
    & sqlpackage /Action:Export "/SourceConnectionString:Server=(localdb)\mssqllocaldb;Database=MatcBuddy;Trusted_Connection=True;TrustServerCertificate=True" "/TargetFile:$bacpac" /Quiet:True
    if ($LASTEXITCODE -ne 0) { throw "Yerel veritabanı dışa aktarılamadı. Hazır bir dosya varsa -BacpacPath ile ver." }
  }
  & sqlpackage /Action:Import "/SourceFile:$bacpac" "/TargetConnectionString:$conn" /Quiet:True
  if ($LASTEXITCODE -ne 0) {
    throw "Veritabanı içe aktarılamadı. Sunucudaki veritabanı boş olmalı (ilk kurulum). Sonraki yüklemelerde -SkipDatabase kullan."
  }
}

# ------------------------------------------------------------------ FTP
function FtpRequest([string]$path, [string]$method) {
  $r = [Net.FtpWebRequest]::Create("ftp://$FtpHost$path")
  $r.Method = $method
  $r.Credentials = New-Object Net.NetworkCredential($FtpUser, $ftpPass)
  $r.EnableSsl = $script:ftpSsl
  $r.UsePassive = $true
  $r.UseBinary = $true
  $r.KeepAlive = $true
  return $r
}
function FtpMkdir([string]$path) {
  try { (FtpRequest $path ([Net.WebRequestMethods+Ftp]::MakeDirectory)).GetResponse().Close() } catch { }
}
function FtpUpload([string]$local, [string]$remote) {
  $r = FtpRequest $remote ([Net.WebRequestMethods+Ftp]::UploadFile)
  $bytes = [IO.File]::ReadAllBytes($local)
  $r.ContentLength = $bytes.Length
  $s = $r.GetRequestStream(); $s.Write($bytes, 0, $bytes.Length); $s.Close()
  $r.GetResponse().Close()
}
function FtpDelete([string]$remote) {
  try { (FtpRequest $remote ([Net.WebRequestMethods+Ftp]::DeleteFile)).GetResponse().Close() } catch { }
}

if (-not $SkipUpload) {
  Step "Dosyalar yükleniyor (FTP)"
  $script:ftpSsl = $true
  try { (FtpRequest "$FtpRoot/" ([Net.WebRequestMethods+Ftp]::ListDirectory)).GetResponse().Close() }
  catch { Write-Host "Şifreli FTP olmadı, düz FTP deneniyor..."; $script:ftpSsl = $false; (FtpRequest "$FtpRoot/" ([Net.WebRequestMethods+Ftp]::ListDirectory)).GetResponse().Close() }

  # Yükleme sırasında siteyi durdur (kilitli DLL'ler serbest kalsın)
  $offline = Join-Path $work "app_offline.htm"
  [IO.File]::WriteAllText($offline, "<html><body style='font-family:sans-serif;background:#070b12;color:#f4f8ec;text-align:center;padding-top:20vh'><h2>MatcBuddy güncelleniyor</h2><p>Birkaç dakika içinde geri döneceğiz.</p></body></html>")
  FtpUpload $offline "$FtpRoot/app_offline.htm"
  Start-Sleep -Seconds 3

  $files = Get-ChildItem $out -Recurse -File
  $dirs = Get-ChildItem $out -Recurse -Directory | Sort-Object { $_.FullName.Length }
  foreach ($d in $dirs) { FtpMkdir ("$FtpRoot/" + $d.FullName.Substring($out.Length + 1).Replace('\', '/')) }
  $i = 0
  foreach ($f in $files) {
    $i++
    $rel = $f.FullName.Substring($out.Length + 1).Replace('\', '/')
    Write-Progress -Activity "FTP" -Status $rel -PercentComplete ([int](100 * $i / $files.Count))
    FtpUpload $f.FullName "$FtpRoot/$rel"
  }
  Write-Progress -Activity "FTP" -Completed
  FtpDelete "$FtpRoot/app_offline.htm"
}

# ------------------------------------------------------------------ Kontrol
Step "Kontrol: $SiteUrl/api/Auth/Providers"
Start-Sleep -Seconds 5
try {
  $res = Invoke-RestMethod "$SiteUrl/api/Auth/Providers" -TimeoutSec 90
  Write-Host ("API çalışıyor. Google: " + [bool]$res.google + ", Facebook: " + [bool]$res.facebook) -ForegroundColor Green
} catch {
  Write-Host "API yanıt vermedi: $($_.Exception.Message)" -ForegroundColor Yellow
  Write-Host "İlk açılış 1-2 dakika sürebilir; tarayıcıda $SiteUrl/api/Auth/Providers adresini tekrar dene."
}
Write-Host ""
Write-Host "Bitti." -ForegroundColor Green
