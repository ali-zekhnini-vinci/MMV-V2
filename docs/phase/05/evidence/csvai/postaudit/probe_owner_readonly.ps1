# Contrôles en LECTURE SEULE des bases persistantes du propriétaire (port 5433) — mandat post-audit CSV-IMPORT-AI-01.
# Mot de passe lu dans les user-secrets existants (jamais affiché), transmis au seul psql par PGPASSWORD, effacé ensuite.
# Session forcée en lecture seule côté serveur (default_transaction_read_only=on) ; aucune écriture, aucun verrou explicite.
param([Parameter(Mandatory)] [string] $OutDir, [Parameter(Mandatory)] [string] $CheckSql)
$ErrorActionPreference = 'Stop'
$psql = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
New-Item -ItemType Directory -Force $OutDir | Out-Null
$targets = @(
    @{ Secrets = 'mmv-v2-api'; Key = 'ConnectionStrings:Mmv'; Database = 'mmv_v2_dev' },
    @{ Secrets = 'mmv-v2-postgresql-tests'; Key = 'ConnectionStrings:MmvTest'; Database = 'mmv_v2_test' }
)
$probe = @"
SELECT 'identite|' || current_database() || '|' || current_user || '|port=' || inet_server_port() || '|version=' || current_setting('server_version') || '|cluster=' || (SELECT system_identifier FROM pg_control_system()) || '|lecture_seule=' || current_setting('transaction_read_only');
SELECT 'migrations|' || count(*) || '|' || string_agg(left("MigrationId", 14) || '_' || split_part("MigrationId", '_', 2), ',' ORDER BY "MigrationId") FROM public."__EFMigrationsHistory";
SELECT 'migration_0009_presente|' || EXISTS (SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = '20261106154105_CsvImportAssistant');
SELECT 'tables_0009_presentes|' || count(*) FROM pg_tables WHERE schemaname = 'imports' AND tablename IN ('batch_originals', 'batch_assistant', 'batch_analyses', 'batch_analysis_events', 'batch_decisions');
SELECT 'autres_sessions|' || count(*) || '|actives=' || count(*) FILTER (WHERE state NOT IN ('idle')) || '|en_transaction=' || count(*) FILTER (WHERE xact_start IS NOT NULL) || '|applications=' || coalesce(string_agg(DISTINCT coalesce(nullif(application_name, ''), '?'), ','), '-') FROM pg_stat_activity WHERE datname = current_database() AND pid <> pg_backend_pid();
SELECT 'ecritures_cumulees|ins=' || tup_inserted || '|maj=' || tup_updated || '|sup=' || tup_deleted || '|commits=' || xact_commit FROM pg_stat_database WHERE datname = current_database();
SELECT 'taille|' || pg_size_pretty(pg_database_size(current_database()));
"@
foreach ($t in $targets) {
    $raw = $null
    foreach ($sid in @($t.Secrets, 'mmv-v2-api', 'mmv-v2-postgresql-tests')) {
        $secretsFile = Join-Path $env:APPDATA "Microsoft\UserSecrets\$sid\secrets.json"
        if (-not $raw -and (Test-Path -LiteralPath $secretsFile)) { $raw = (Get-Content -LiteralPath $secretsFile -Raw -Encoding UTF8 | ConvertFrom-Json).($t.Key) }
    }
    if (-not $raw) { Write-Output "$($t.Database)|cle absente"; continue }
    $b = [System.Data.Common.DbConnectionStringBuilder]::new(); $b.set_ConnectionString($raw)
    $get = { param($names) foreach ($n in $names) { if ($b.ContainsKey($n)) { return [string]$b[$n] } }; return $null }
    $hostName = & $get @('Host', 'Server'); $port = & $get @('Port'); $db = & $get @('Database'); $user = & $get @('Username', 'User ID', 'User')
    if ($hostName -notin @('localhost', '127.0.0.1', '::1') -or $port -ne '5433' -or $db -ne $t.Database -or $user -ne $t.Database) { Write-Output "$($t.Database)|cible refusee"; continue }
    $env:PGPASSWORD = & $get @('Password')
    $env:PGOPTIONS = '-c default_transaction_read_only=on -c statement_timeout=30s -c lock_timeout=2s'
    try {
        $common = @('-h', 'localhost', '-p', '5433', '-U', $t.Database, '-d', $t.Database, '-X', '-A', '-t', '-v', 'ON_ERROR_STOP=1')
        if ($env:MMV_PROBE_SQL) { & $psql @common -f $env:MMV_PROBE_SQL | ForEach-Object { "$($t.Database)|$_" }; continue }
        $probe | & $psql @common | ForEach-Object { "$($t.Database)|$_" }
        Start-Sleep -Seconds 5
        "SELECT 'ecritures_cumulees_5s_plus_tard|ins=' || tup_inserted || '|maj=' || tup_updated || '|sup=' || tup_deleted || '|commits=' || xact_commit FROM pg_stat_database WHERE datname = current_database();" | & $psql @common | ForEach-Object { "$($t.Database)|$_" }
        & $psql @common -f $CheckSql | Out-File -Encoding utf8 (Join-Path $OutDir "$($t.Database).check.txt")
        "$($t.Database)|check_exit=$LASTEXITCODE"
    }
    finally { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue; Remove-Item Env:PGOPTIONS -ErrorAction SilentlyContinue }
}
