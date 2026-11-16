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
  [switch]$SkipUpload,
  # Sunucudaki veritabanında eski bir kopya varsa içe aktarmadan önce tüm nesneleri siler (GERİ ALINAMAZ)
  [switch]$ResetDatabase,
  # Veritabanı dışa aktarılmadan hemen önce bu bilgisayardaki canlı API'yi durdurur (yeni kayıt kaybolmasın).
  # Taşıma hata verirse yerel canlı API kendiliğinden yeniden başlatılır.
  [switch]$StopLive,
  # Yalnızca appsettings.Production.json yüklenir (ayar değişikliği; uygulama kendiliğinden yeniden başlar)
  [switch]$ConfigOnly,
  # Bu bilgisayardaki canlı kurulum (scripts\selfhost): varsa JWT anahtarı, e-posta ayarı, fotoğraflar
  # ve bildirim anahtarı oradan alınır (oturumlar düşmesin, son yüklenen fotoğraflar kaybolmasın).
  [string]$LiveRoot = "D:\MatcBuddyLive"
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

$script:liveStopped = $false
trap {
  if ($script:curlCfg) { Remove-Item $script:curlCfg -Force -ErrorAction SilentlyContinue }
  if ($script:liveStopped) {
    Enable-ScheduledTask -TaskName "MatcBuddy Live" | Out-Null
    Start-ScheduledTask -TaskName "MatcBuddy Live"
    Write-Host "Taşıma tamamlanamadı; bu bilgisayardaki canlı API yeniden başlatıldı." -ForegroundColor Yellow
  }
  break
}
function Plain([Security.SecureString]$s) { [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)) }

# ------------------------------------------------------------------ Bilgiler
Step "Bilgiler (MonsterASP panelinden kopyala)"
$conn = Plain (Read-Host "Veritabanı bağlantı dizesi (Databases > bağlantı bilgisi; şifre dahil)" -AsSecureString)
if ([string]::IsNullOrWhiteSpace($conn)) { throw "Bağlantı dizesi boş olamaz." }
if ($conn -notmatch "TrustServerCertificate") { $conn = $conn.TrimEnd(';') + ";TrustServerCertificate=True" }
# Panelin "Local access" adresine yalnızca barındırılan siteden bağlanılabilir; veritabanı bu bilgisayardan
# taşınırken "Remote access" sekmesindeki bağlantı dizesi gerekir.
$importConn = $conn
if (-not $SkipDatabase) {
  $remote = Plain (Read-Host "Uzaktan erişim (Remote access) bağlantı dizesi - veritabanını bu bilgisayardan taşımak için" -AsSecureString)
  if (-not [string]::IsNullOrWhiteSpace($remote)) {
    $importConn = $remote
    if ($importConn -notmatch "TrustServerCertificate") { $importConn = $importConn.TrimEnd(';') + ";TrustServerCertificate=True" }
  }
  if ($importConn -match '\*{3,}') { throw "Bağlantı dizesinde şifre yerine yıldızlar var. Panelde 'Show with password' ile şifreli dizeyi kopyala." }
  # Hiçbir şeye dokunmadan önce giriş bilgilerini doğrula
  Write-Host "Sunucu veritabanına bağlantı deneniyor..."
  $probe = New-Object Data.SqlClient.SqlConnection $importConn
  try { $probe.Open(); Write-Host "Bağlantı başarılı." -ForegroundColor Green }
  catch { throw "Sunucu veritabanına bağlanılamadı: $($_.Exception.Message) - Remote access sekmesindeki sunucu/kullanıcı/şifreyi kontrol et." }
  finally { $probe.Close() }
}
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
$liveSettingsFile = Join-Path $LiveRoot "app\appsettings.Production.json"
$live = $null
if (Test-Path $liveSettingsFile) { $live = Get-Content $liveSettingsFile -Raw | ConvertFrom-Json }
if (-not (Test-Path $jwtFile) -and $live -and $live.Jwt.Key) {
  # Canlı kurulumun anahtarı kullanılır: mevcut kullanıcı oturumları taşımadan sonra da geçerli kalır
  [IO.File]::WriteAllText($jwtFile, $live.Jwt.Key)
}
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
if (-not $fbSecret -and $live -and $live.Auth.Facebook.AppSecret) { $fbSecret = $live.Auth.Facebook.AppSecret }

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
# Canlı kurulumda yapılandırılmış e-posta gönderimi (configure-messaging.ps1) aynen taşınır
if ($live -and $live.Email) { $prod.Email = $live.Email }
$prodJson = $prod | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText((Join-Path $out "appsettings.Production.json"), $prodJson, (New-Object Text.UTF8Encoding($false)))

# Yüklenmiş fotoğraflar ve bildirim (VAPID) anahtarı — canlı kurulum varsa oradan (en güncel kopya)
$appData = Join-Path $api "App_Data"
$liveSources = @{ "media" = (Join-Path $LiveRoot "data\media"); "vapid.json" = (Join-Path $LiveRoot "app\App_Data\vapid.json") }
foreach ($item in @("media", "vapid.json")) {
  $src = Join-Path $appData $item
  if (Test-Path $liveSources[$item]) { $src = $liveSources[$item] }
  if (Test-Path $src) {
    New-Item -ItemType Directory -Force (Join-Path $out "App_Data") | Out-Null
    Copy-Item $src (Join-Path $out "App_Data") -Recurse -Force
  }
}

# ------------------------------------------------------------------ FTP bağlantısı
# .NET FtpWebRequest pasif modda ara sıra "227 Entering Passive Mode" hatasıyla kopuyordu;
# Windows'un yerleşik curl.exe'si kullanılır (dosya başına yeniden deneme).
# Şifre komut satırına yazılmaz: geçici curl ayar dosyasından okunur ve iş bitince silinir.
$script:curlCfg = $null
function FtpUrl([string]$path) {
  $enc = ($path.TrimStart('/') -split '/' | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/'
  return "ftp://$FtpHost/$enc"
}
function Invoke-Curl([string[]]$curlArgs) {
  $base = @("-sS", "-K", $script:curlCfg, "--ftp-pasv", "--connect-timeout", "30")
  # Windows Schannel + TLS 1.3 şifreli FTP veri bağlantısında SEC_E_CONTEXT_EXPIRED (curl 18) veriyor → TLS 1.2
  if ($script:ftpSsl) { $base += @("--ssl-reqd", "--ssl-no-revoke", "--tls-max", "1.2") }
  & curl.exe @base @curlArgs
  return $LASTEXITCODE
}
function FtpUpload([string]$local, [string]$remote) {
  for ($attempt = 1; $attempt -le 3; $attempt++) {
    $code = Invoke-Curl @("--ftp-create-dirs", "--retry", "3", "--retry-delay", "3", "--retry-all-errors", "-T", $local, (FtpUrl $remote))
    if ($code -eq 0) { return }
    Write-Host "  tekrar deneniyor ($attempt/3): $remote" -ForegroundColor Yellow
    Start-Sleep -Seconds (5 * $attempt)
  }
  throw "Yüklenemedi: $remote (curl çıkış kodu $code)"
}
function FtpDelete([string]$remote) {
  [void](Invoke-Curl @("-Q", "DELE $remote", "-o", "NUL", "ftp://$FtpHost/"))
}

$offline = Join-Path $work "app_offline.htm"
[IO.File]::WriteAllText($offline, "<html><body style='font-family:sans-serif;background:#070b12;color:#f4f8ec;text-align:center;padding-top:20vh'><h2>MatcBuddy güncelleniyor</h2><p>Birkaç dakika içinde geri döneceğiz.</p></body></html>")
if (-not $SkipUpload) {
  $script:curlCfg = Join-Path $work "ftp.cfg"
  $pwEsc = $ftpPass.Replace('\', '\\').Replace('"', '\"')
  [IO.File]::WriteAllText($script:curlCfg, "user = `"$($FtpUser):$pwEsc`"`n", (New-Object Text.UTF8Encoding($false)))
  $script:ftpSsl = $true
  if ((Invoke-Curl @("--list-only", "-o", "NUL", ((FtpUrl $FtpRoot) + "/"))) -ne 0) {
    Write-Host "Şifreli FTP olmadı, düz FTP deneniyor..."
    $script:ftpSsl = $false
    if ((Invoke-Curl @("--list-only", "-o", "NUL", ((FtpUrl $FtpRoot) + "/"))) -ne 0) { throw "FTP'ye bağlanılamadı (sunucu / kullanıcı / şifre?)." }
  }
}

# ------------------------------------------------------------------ Veritabanı
if (-not $SkipDatabase) {
  if ($StopLive) {
    Step "Bu bilgisayardaki canlı API durduruluyor"
    Disable-ScheduledTask -TaskName "MatcBuddy Live" | Out-Null
    $script:liveStopped = $true
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "selfhost\stop.ps1")
  }
  Step "Veritabanı taşınıyor (yerel demo verileri -> sunucu)"
  if ($BacpacPath) {
    if (-not (Test-Path $BacpacPath)) { throw "Bacpac dosyası bulunamadı: $BacpacPath" }
    $bacpac = $BacpacPath
  } else {
    $bacpac = Join-Path $work "MatcBuddy.bacpac"
    & sqlpackage /Action:Export "/SourceConnectionString:Server=(localdb)\mssqllocaldb;Database=MatcBuddy;Trusted_Connection=True;TrustServerCertificate=True" "/TargetFile:$bacpac" /Quiet:True
    if ($LASTEXITCODE -ne 0) { throw "Yerel veritabanı dışa aktarılamadı. Hazır bir dosya varsa -BacpacPath ile ver." }
  }
  if ($script:curlCfg) {
    # Sunucudaki uygulama içe aktarma sırasında çalışırsa boş tablolara varsayılan kayıt (ör. e-posta şablonları)
    # ekleyip içe aktarmayı çakıştırıyor: taşıma bitene kadar "bakımda" sayfasıyla durdurulur.
    Step "Sunucudaki uygulama bakım moduna alınıyor"
    FtpUpload $offline "$FtpRoot/app_offline.htm"
    Start-Sleep -Seconds 10
  }
  if ($ResetDatabase) {
    Step "Sunucudaki eski veritabanı nesneleri siliniyor"
    $reset = @"
DECLARE @sql nvarchar(max);
SET @sql = N''; SELECT @sql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(parent_object_id)) + N' DROP CONSTRAINT ' + QUOTENAME(name) + N';' FROM sys.foreign_keys; EXEC sp_executesql @sql;
SET @sql = N''; SELECT @sql += N'DROP VIEW ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';' FROM sys.views WHERE is_ms_shipped = 0; EXEC sp_executesql @sql;
SET @sql = N''; SELECT @sql += N'DROP PROCEDURE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';' FROM sys.procedures WHERE is_ms_shipped = 0; EXEC sp_executesql @sql;
SET @sql = N''; SELECT @sql += N'DROP FUNCTION ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';' FROM sys.objects WHERE type IN ('FN','IF','TF') AND is_ms_shipped = 0; EXEC sp_executesql @sql;
SET @sql = N''; SELECT @sql += N'DROP TABLE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';' FROM sys.tables WHERE is_ms_shipped = 0; EXEC sp_executesql @sql;
SET @sql = N''; SELECT @sql += N'DROP SEQUENCE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';' FROM sys.sequences; EXEC sp_executesql @sql;
SET @sql = N''; SELECT @sql += N'DROP TYPE ' + QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name) + N';' FROM sys.types WHERE is_user_defined = 1; EXEC sp_executesql @sql;
SELECT COUNT(*) FROM sys.objects WHERE is_ms_shipped = 0 AND type IN ('U','V','P','FN','IF','TF','SO');
"@
    $cn = New-Object Data.SqlClient.SqlConnection $importConn
    $cn.Open()
    try {
      $cmd = $cn.CreateCommand(); $cmd.CommandText = $reset; $cmd.CommandTimeout = 300
      $left = $cmd.ExecuteScalar()
    } finally { $cn.Close() }
    if ($left -ne 0) { throw "Sunucudaki veritabanı temizlenemedi ($left nesne kaldı)." }
    Write-Host "Temizlendi."
  }
  & sqlpackage /Action:Import "/SourceFile:$bacpac" "/TargetConnectionString:$importConn" /Quiet:True
  if ($LASTEXITCODE -ne 0) {
    throw "Veritabanı içe aktarılamadı. Sunucudaki veritabanı boş olmalı (ilk kurulum). Sonraki yüklemelerde -SkipDatabase kullan."
  }
}

# ------------------------------------------------------------------ FTP
if (-not $SkipUpload) {
  Step "Dosyalar yükleniyor (FTP)"
  try {
    if ($ConfigOnly) {
      FtpUpload (Join-Path $out "appsettings.Production.json") "$FtpRoot/appsettings.Production.json"
      Write-Host "  appsettings.Production.json yüklendi"
      FtpDelete "$FtpRoot/app_offline.htm"
    } else {
    # Yükleme sırasında siteyi durdur (kilitli DLL'ler serbest kalsın)
    FtpUpload $offline "$FtpRoot/app_offline.htm"
    Start-Sleep -Seconds 3

    $files = @(Get-ChildItem $out -Recurse -File)
    $i = 0
    foreach ($f in $files) {
      $i++
      $rel = $f.FullName.Substring($out.Length + 1).Replace('\', '/')
      FtpUpload $f.FullName "$FtpRoot/$rel"
      if ($i % 25 -eq 0 -or $i -eq $files.Count) { Write-Host ("  {0}/{1} dosya yüklendi" -f $i, $files.Count) }
    }
    FtpDelete "$FtpRoot/app_offline.htm"
    }
  } finally {
    Remove-Item $script:curlCfg -Force -ErrorAction SilentlyContinue
  }
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
