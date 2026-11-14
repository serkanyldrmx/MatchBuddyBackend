<#
.SYNOPSIS
  Canlı API için e-posta (SMTP) ve isteğe bağlı SMS (Netgsm) ayarlarını yapar; "şifremi unuttum" kodları gerçekten gider.

.DESCRIPTION
  Bu ayarlar yapılmazsa API kodları göndermez, D:\MatcBuddyLive\data\outbox\*.txt dosyasına yazar
  (kullanıcıya hiçbir şey gelmez). Betik sorduğu değerleri app\appsettings.Production.json içine yazar
  ve API'yi yeniden başlatır (start.ps1 gözetmeni birkaç saniye içinde yeniden açar).

  Gmail ile en kolay yol:
    1) Google hesabında 2 adımlı doğrulamayı aç.
    2) https://myaccount.google.com/apppasswords adresinden "MatcBuddy" adlı bir uygulama şifresi oluştur (16 harf).
    3) Bu betikte: Host = smtp.gmail.com, Port = 587, Kullanıcı = gmail adresin, Şifre = uygulama şifresi,
       Gönderen = aynı gmail adresi.
  Kendi alan adın (ör. info@matcbuddy.com) için alan adı sağlayıcının SMTP bilgilerini gir.

  Şifreler yalnızca bu bilgisayardaki ayar dosyasına yazılır, depoya girmez.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\selfhost\configure-messaging.ps1
.EXAMPLE
  # Gönderimi denemek için (ayarlar kaydedildikten sonra bir test e-postası yollar)
  powershell -ExecutionPolicy Bypass -File scripts\selfhost\configure-messaging.ps1 -TestTo ben@ornek.com
#>
param(
    [string]$Root = "D:\MatcBuddyLive",
    [string]$TestTo = ""
)

$ErrorActionPreference = "Stop"
$app = Join-Path $Root "app"
$prodFile = Join-Path $app "appsettings.Production.json"
if (-not (Test-Path $prodFile)) { throw "Bulunamadı: $prodFile (önce publish.ps1 çalıştırılmalı)" }

function Ask([string]$label, [string]$default = "") {
    $suffix = if ($default) { " [$default]" } else { "" }
    $v = Read-Host "$label$suffix"
    if ([string]::IsNullOrWhiteSpace($v)) { return $default }
    return $v.Trim()
}

function AskSecret([string]$label) {
    $s = Read-Host $label -AsSecureString
    $b = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($s)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($b) } finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($b) }
}

$json = Get-Content $prodFile -Raw | ConvertFrom-Json
function Ensure($obj, [string]$name) {
    if (-not ($obj.PSObject.Properties.Name -contains $name)) { $obj | Add-Member -NotePropertyName $name -NotePropertyValue ([pscustomobject]@{}) }
    return $obj.$name
}
function SetProp($obj, [string]$name, $value) {
    if ($obj.PSObject.Properties.Name -contains $name) { $obj.$name = $value }
    else { $obj | Add-Member -NotePropertyName $name -NotePropertyValue $value }
}

Write-Host ""
Write-Host "== E-posta (SMTP) ==" -ForegroundColor Cyan
$email = Ensure $json "Email"
$smtp = Ensure $email "Smtp"
$host_ = Ask "SMTP sunucusu" ($(if ($smtp.Host) { $smtp.Host } else { "smtp.gmail.com" }))
$port = [int](Ask "Port" ($(if ($smtp.Port) { "$($smtp.Port)" } else { "587" })))
$user = Ask "Kullanıcı (genelde e-posta adresi)" $smtp.User
$pass = AskSecret "Şifre / uygulama şifresi (boş bırakırsan mevcut korunur)"
$from = Ask "Gönderen adres" ($(if ($smtp.From) { $smtp.From } else { $user }))
$fromName = Ask "Gönderen adı" ($(if ($smtp.FromName) { $smtp.FromName } else { "MatcBuddy" }))

SetProp $smtp "Host" $host_
SetProp $smtp "Port" $port
SetProp $smtp "User" $user
if ($pass) { SetProp $smtp "Password" ($pass -replace '\s', '') }
SetProp $smtp "From" $from
SetProp $smtp "FromName" $fromName
SetProp $smtp "UseSsl" $true

Write-Host ""
$wantSms = Ask "SMS (Netgsm) de ayarlansın mı? (e/h)" "h"
if ($wantSms -match '^[eEyY]') {
    Write-Host "== SMS (Netgsm) ==" -ForegroundColor Cyan
    $sms = Ensure $json "Sms"
    $netgsm = Ensure $sms "Netgsm"
    SetProp $netgsm "UserCode" (Ask "Netgsm kullanıcı kodu" $netgsm.UserCode)
    $np = AskSecret "Netgsm şifresi (boş bırakırsan mevcut korunur)"
    if ($np) { SetProp $netgsm "Password" $np }
    SetProp $netgsm "Header" (Ask "Onaylı gönderici başlığı (msgheader)" $netgsm.Header)
}

[IO.File]::WriteAllText($prodFile, ($json | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
Write-Host ""
Write-Host "Kaydedildi: $prodFile" -ForegroundColor Green

# İsteğe bağlı: doğrudan SMTP testi (API'den bağımsız)
if ($TestTo) {
    Write-Host "Test e-postası gönderiliyor: $TestTo"
    try {
        $client = New-Object Net.Mail.SmtpClient($host_, $port)
        $client.EnableSsl = $true
        $effectivePass = if ($pass) { $pass -replace '\s', '' } else { $smtp.Password }
        $client.Credentials = New-Object Net.NetworkCredential($user, $effectivePass)
        $msg = New-Object Net.Mail.MailMessage($from, $TestTo, "MatcBuddy test", "SMTP ayarı çalışıyor.")
        $client.Send($msg)
        Write-Host "Test e-postası gönderildi." -ForegroundColor Green
    } catch {
        Write-Host "Test başarısız: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "Gmail kullanıyorsan normal şifre değil, uygulama şifresi gerekir."
    }
}

# API'yi yeniden başlat: gözetmen (start.ps1) 30 sn içinde yeniden açar
$exe = Join-Path $app "MatcBuddy.Api.exe"
$p = Get-Process MatcBuddy.Api -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }
if ($p) {
    $p | Stop-Process -Force
    Write-Host "API durduruldu; gözetmen birkaç saniye içinde yeni ayarlarla yeniden başlatacak."
} else {
    Write-Host "Çalışan API yok; start.ps1 ile başlatabilirsin."
}
