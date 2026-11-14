<#
  Yerel veritabanındaki tüm verileri INSERT komutlarından oluşan bir .sql dosyasına döker.
  Dışarıdan bağlanılamayan barındırmalarda (MonsterASP ücretsiz plan) veritabanını panelin "Import SQL" özelliğiyle
  doldurmak için: önce şema (dotnet ef migrations script --idempotent), sonra bu dosya.

  Kullanım:
    powershell -ExecutionPolicy Bypass -File scripts\export-data-sql.ps1 -OutFile D:\Project\MatcBuddyDeploy\data.sql
#>
param(
  [string]$ConnectionString = "Server=(localdb)\mssqllocaldb;Database=MatcBuddy;Trusted_Connection=True;TrustServerCertificate=True",
  [Parameter(Mandatory = $true)][string]$OutFile,
  [int]$BatchRows = 200
)
$ErrorActionPreference = "Stop"
$inv = [Globalization.CultureInfo]::InvariantCulture

$conn = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
$conn.Open()
function Query([string]$sql) {
  $cmd = $conn.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = 300
  $t = New-Object System.Data.DataTable; $t.Load($cmd.ExecuteReader()); return ,$t
}

$tables = Query @"
SELECT s.name AS SchemaName, t.name AS TableName,
       CAST(OBJECTPROPERTY(t.object_id, 'TableHasIdentity') AS int) AS HasIdentity
FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0 AND t.name NOT IN ('__EFMigrationsHistory', 'sysdiagrams')
ORDER BY t.name
"@

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("-- MatcBuddy demo verileri ($(Get-Date -Format 'yyyy-MM-dd HH:mm')). Önce şema betiği çalıştırılmış olmalı.")
[void]$sb.AppendLine("SET NOCOUNT ON;")
foreach ($t in $tables.Rows) { [void]$sb.AppendLine("ALTER TABLE [$($t.SchemaName)].[$($t.TableName)] NOCHECK CONSTRAINT ALL;") }
[void]$sb.AppendLine("GO")

function Lit($value, [string]$type) {
  if ($value -is [DBNull] -or $null -eq $value) { return "NULL" }
  switch -Regex ($type) {
    '^(bit)$' { return $(if ($value) { "1" } else { "0" }) }
    '^(tinyint|smallint|int|bigint)$' { return ([string]$value) }
    '^(decimal|numeric|money|smallmoney|float|real)$' { return ([Convert]::ToString($value, $inv)) }
    '^(datetime|smalldatetime)$' { return "'" + ([datetime]$value).ToString("yyyy-MM-ddTHH:mm:ss.fff", $inv) + "'" }
    '^(datetime2|date|time)$' {
      if ($value -is [TimeSpan]) { return "'" + $value.ToString() + "'" }
      if ($type -eq 'date') { return "'" + ([datetime]$value).ToString("yyyy-MM-dd", $inv) + "'" }
      return "'" + ([datetime]$value).ToString("yyyy-MM-ddTHH:mm:ss.fffffff", $inv) + "'"
    }
    '^(datetimeoffset)$' { return "'" + ([DateTimeOffset]$value).ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz", $inv) + "'" }
    '^(uniqueidentifier)$' { return "'" + $value.ToString() + "'" }
    '^(varbinary|binary|image|timestamp|rowversion)$' { return "0x" + (($value | ForEach-Object { $_.ToString("X2") }) -join "") }
    default { return "N'" + ([string]$value).Replace("'", "''") + "'" }
  }
}

$total = 0
foreach ($t in $tables.Rows) {
  $full = "[$($t.SchemaName)].[$($t.TableName)]"
  $cols = Query @"
SELECT c.name, ty.name AS TypeName
FROM sys.columns c JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('$full') AND c.is_computed = 0 AND ty.name NOT IN ('timestamp','rowversion')
ORDER BY c.column_id
"@
  $names = @($cols.Rows | ForEach-Object { $_.name })
  $types = @($cols.Rows | ForEach-Object { $_.TypeName })
  $colList = ($names | ForEach-Object { "[$_]" }) -join ", "
  $data = Query "SELECT $colList FROM $full"
  if ($data.Rows.Count -eq 0) { continue }
  [void]$sb.AppendLine("-- $full ($($data.Rows.Count) satır)")
  if ($t.HasIdentity -eq 1) { [void]$sb.AppendLine("SET IDENTITY_INSERT $full ON;") }
  $i = 0
  foreach ($row in $data.Rows) {
    if ($i % $BatchRows -eq 0) {
      if ($i -gt 0) { [void]$sb.AppendLine(";") }
      [void]$sb.Append("INSERT INTO $full ($colList) VALUES")
      $sep = "`n"
    } else { $sep = ",`n" }
    $vals = for ($k = 0; $k -lt $names.Count; $k++) { Lit $row[$k] $types[$k] }
    [void]$sb.Append($sep + "(" + ($vals -join ", ") + ")")
    $i++
  }
  [void]$sb.AppendLine(";")
  if ($t.HasIdentity -eq 1) { [void]$sb.AppendLine("SET IDENTITY_INSERT $full OFF;") }
  [void]$sb.AppendLine("GO")
  $total += $data.Rows.Count
}

foreach ($t in $tables.Rows) { [void]$sb.AppendLine("ALTER TABLE [$($t.SchemaName)].[$($t.TableName)] CHECK CONSTRAINT ALL;") }
[void]$sb.AppendLine("GO")
$conn.Close()

[IO.File]::WriteAllText($OutFile, $sb.ToString(), (New-Object Text.UTF8Encoding($true)))
Write-Host "$total satır yazıldı: $OutFile"
