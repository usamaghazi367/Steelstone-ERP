$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$localSettings = Join-Path $root "ConstFire.Backend\appsettings.Production.local.json"
if (-not (Test-Path $localSettings)) { throw "Missing $localSettings" }

$settings = Get-Content $localSettings -Raw | ConvertFrom-Json
$connStr = $settings.ConnectionStrings.DefaultConnection

$sql = @"
UPDATE r
SET RecordCode = CASE m.Code
  WHEN '01' THEN 'ENT-' + RIGHT('000000' + CAST(r.Id AS varchar(10)), 6)
  WHEN '02' THEN 'MFR-' + RIGHT('000000' + CAST(r.Id AS varchar(10)), 6)
  WHEN '03' THEN 'DST-' + RIGHT('000000' + CAST(r.Id AS varchar(10)), 6)
  WHEN '04' THEN 'CUS-' + RIGHT('000000' + CAST(r.Id AS varchar(10)), 6)
  WHEN '05' THEN 'TRN-' + RIGHT('000000' + CAST(r.Id AS varchar(10)), 6)
  WHEN '06' THEN 'VDR-' + RIGHT('000000' + CAST(r.Id AS varchar(10)), 6)
  WHEN '07' THEN 'EMP-' + RIGHT('000000' + CAST(r.Id AS varchar(10)), 6)
  WHEN '08' THEN 'BIL-' + RIGHT('000000' + CAST(r.Id AS varchar(10)), 6)
  WHEN '09' THEN 'QOT-' + RIGHT('000000' + CAST(r.Id AS varchar(10)), 6)
  WHEN '10' THEN 'PAY-' + RIGHT('000000' + CAST(r.Id AS varchar(10)), 6)
  WHEN '11' THEN 'INV-' + RIGHT('000000' + CAST(r.Id AS varchar(10)), 6)
  WHEN '12' THEN 'PRL-' + RIGHT('000000' + CAST(r.Id AS varchar(10)), 6)
  WHEN '13' THEN 'ITM-' + RIGHT('000000' + CAST(r.Id AS varchar(10)), 6)
  WHEN '14' THEN 'CHL-' + RIGHT('000000' + CAST(r.Id AS varchar(10)), 6)
  WHEN '15' THEN 'RCT-' + RIGHT('000000' + CAST(r.Id AS varchar(10)), 6)
  ELSE 'MOD' + m.Code + '-' + RIGHT('000000' + CAST(r.Id AS varchar(10)), 6)
END
FROM ErpRecords r
INNER JOIN ErpModules m ON m.Id = r.ModuleId
WHERE r.RecordCode IS NULL OR LTRIM(RTRIM(r.RecordCode)) = '';

SELECT @@ROWCOUNT AS RowsUpdated;
"@

$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()
try {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    $updated = [int]$cmd.ExecuteScalar()
    Write-Host "Backfilled $updated record(s) with RecordCode."
} finally {
    $conn.Close()
}
