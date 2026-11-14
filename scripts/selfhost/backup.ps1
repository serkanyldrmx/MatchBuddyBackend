<#
.SYNOPSIS
  MatcBuddy veritabanını D:\MatcBuddyLive\backups altına yedekler, 14 günden eskileri siler
  ve OneDrive varsa bir kopyasını OneDrive\MatcBuddyBackups klasörüne koyar.
#>
param(
    [string]$Root = "D:\MatcBuddyLive",
    [string]$Database = "MatcBuddy",
    [int]$KeepDays = 14
)

$ErrorActionPreference = "Stop"
$dir = Join-Path $Root "backups"
New-Item -ItemType Directory -Force $dir | Out-Null
$file = Join-Path $dir ("{0}_{1:yyyyMMdd}.bak" -f $Database, (Get-Date))

$conn = New-Object System.Data.SqlClient.SqlConnection "Server=(localdb)\mssqllocaldb;Database=master;Trusted_Connection=True"
$conn.Open()
try {
    $cmd = $conn.CreateCommand()
    $cmd.CommandTimeout = 600
    $cmd.CommandText = "BACKUP DATABASE [$Database] TO DISK = @f WITH INIT, COPY_ONLY, CHECKSUM"
    [void]$cmd.Parameters.AddWithValue("@f", $file)
    [void]$cmd.ExecuteNonQuery()
} finally { $conn.Close() }

Get-ChildItem $dir -Filter "${Database}_*.bak" | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$KeepDays) } | Remove-Item -Force

if ($env:OneDrive -and (Test-Path $env:OneDrive)) {
    $off = Join-Path $env:OneDrive "MatcBuddyBackups"
    New-Item -ItemType Directory -Force $off | Out-Null
    Copy-Item $file $off -Force
    Get-ChildItem $off -Filter "${Database}_*.bak" | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$KeepDays) } | Remove-Item -Force
}
Write-Host "Yedek: $file"
