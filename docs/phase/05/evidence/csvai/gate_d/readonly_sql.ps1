# Execute un script SQL en LECTURE SEULE (default_transaction_read_only=on) sur UNE base du proprietaire (port 5433).
# Chaine de connexion lue dans les user-secrets (jamais affichee), mot de passe transmis au seul psql puis efface.
param([Parameter(Mandatory)] [ValidateSet('mmv_v2_dev', 'mmv_v2_test')] [string] $Database, [Parameter(Mandatory)] [string] $Sql, [string] $Out)
$ErrorActionPreference = 'Stop'
$psql = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
$key = if ($Database -eq 'mmv_v2_dev') { 'ConnectionStrings:Mmv' } else { 'ConnectionStrings:MmvTest' }
$raw = $null
foreach ($sid in 'mmv-v2-api', 'mmv-v2-postgresql-tests') {
    $f = Join-Path $env:APPDATA "Microsoft\UserSecrets\$sid\secrets.json"
    if (-not $raw -and (Test-Path -LiteralPath $f)) { $raw = (Get-Content -LiteralPath $f -Raw -Encoding UTF8 | ConvertFrom-Json).($key) }
}
$b = [System.Data.Common.DbConnectionStringBuilder]::new(); $b.set_ConnectionString($raw)
$get = { param($names) foreach ($n in $names) { if ($b.ContainsKey($n)) { return [string]$b[$n] } }; return $null }
if ((& $get @('Host', 'Server')) -notin @('localhost', '127.0.0.1', '::1') -or (& $get @('Port')) -ne '5433' -or (& $get @('Database')) -ne $Database -or (& $get @('Username', 'User ID', 'User')) -ne $Database) { throw "cible refusee $Database" }
$env:PGCLIENTENCODING = 'UTF8'
$env:PGPASSWORD = & $get @('Password')
$env:PGOPTIONS = '-c default_transaction_read_only=on -c statement_timeout=60s -c lock_timeout=2s'
try {
    $res = & $psql -h localhost -p 5433 -U $Database -d $Database -X -A -t -v ON_ERROR_STOP=1 -f $Sql
    $rc = $LASTEXITCODE
    if ($Out) { $res | Set-Content -Encoding utf8 $Out } else { $res }
    "exit=$rc"
}
finally { Remove-Item Env:PGPASSWORD, Env:PGOPTIONS -ErrorAction SilentlyContinue }
