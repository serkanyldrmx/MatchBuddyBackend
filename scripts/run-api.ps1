<#
.SYNOPSIS
  API'yi yerelde başlatır (http://localhost:5033, Development).

.DESCRIPTION
  Proje .NET 10 hedefler (global.json: SDK 10.0.100+). "C:\Program Files\dotnet" altında yalnızca .NET 9 SDK
  varsa düz "dotnet run" "A compatible .NET SDK was not found" hatası verir. Bu betik .NET 10 SDK'sını
  (önce kullanıcı klasöründeki %USERPROFILE%\.dotnet, sonra Program Files) bulup o oturum için kullanır.

.PARAMETER Migrate
  Başlatmadan önce bekleyen EF migration'larını yerel veritabanına uygular.

.PARAMETER Origins
  CORS için izin verilecek ek ön yüz adresleri (varsayılan: http://localhost:3000 ve :3001).

.EXAMPLE
  .\scripts\run-api.ps1
  .\scripts\run-api.ps1 -Migrate
#>
param(
    [switch]$Migrate,
    [string[]]$Origins = @('http://localhost:3000', 'http://localhost:3001')
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

# .NET 10 SDK'sı olan dotnet kurulumunu bul
$candidates = @("$env:USERPROFILE\.dotnet", "$env:ProgramFiles\dotnet") | Where-Object { Test-Path "$_\dotnet.exe" }
$root = $candidates | Where-Object { Get-ChildItem "$_\sdk" -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -like '10.*' } } | Select-Object -First 1
if (-not $root) {
    Write-Host ".NET 10 SDK bulunamadı. Kurmak için: winget install Microsoft.DotNet.SDK.10" -ForegroundColor Red
    exit 1
}
$env:DOTNET_ROOT = $root
$env:PATH = "$root;$env:PATH"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
for ($i = 0; $i -lt $Origins.Count; $i++) { Set-Item "env:Cors__AllowedOrigins__$i" $Origins[$i] }
Write-Host "dotnet: $root (SDK $(& "$root\dotnet.exe" --version))" -ForegroundColor DarkGray

Push-Location $repo
try {
    if ($Migrate) {
        & "$root\dotnet.exe" tool restore | Out-Null
        & "$root\dotnet.exe" ef database update --project src/MatcBuddy.DataAccess --startup-project src/MatcBuddy.Api
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    & "$root\dotnet.exe" run --project src/MatcBuddy.Api --launch-profile http
}
finally {
    Pop-Location
}
