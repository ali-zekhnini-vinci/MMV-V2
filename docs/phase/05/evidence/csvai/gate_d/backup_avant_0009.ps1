# G05-SQL-0009 (mandat GO conditionnel du 07/11/2026, section 2) : sauvegardes DEFINITIVES avant application.
# Lecture seule sur les bases du proprietaire (session forcee default_transaction_read_only=on).
# Pour chaque base : identite + 0 autre session exigee (sinon STOP), pg_dump -Fc (code de sortie verifie), SHA-256,
# pg_restore --list, empreinte de coherence 0009_restore_check.sql de la base source, puis restauration de la sauvegarde
# sur une base NEUVE du cluster JETABLE (port 55434, jamais 5433) et comparaison des empreintes (IDENTIQUE exige, sinon STOP).
# Les copies restaurees et les roles miroirs sont ensuite supprimes du cluster jetable. Aucun secret affiche.
param([Parameter(Mandatory)] [string] $Work, [Parameter(Mandatory)] [string] $CheckSql, [Parameter(Mandatory)] [string] $IdentitySql)
$ErrorActionPreference = 'Stop'
$bin = 'C:\Program Files\PostgreSQL\18\bin'
$dir = Join-Path $env:USERPROFILE 'Documents\MMV-V2-sauvegardes'
New-Item -ItemType Directory -Force $Work | Out-Null
$env:PGCLIENTENCODING = 'UTF8'
$stamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd_HHmmss')
function Scratch([string] $db, [string] $sql) { & "$bin\psql.exe" -h 127.0.0.1 -p 55434 -U postgres -d $db -X -A -t -v ON_ERROR_STOP=1 -c $sql; if ($LASTEXITCODE -ne 0) { throw "psql jetable $LASTEXITCODE" } }
$scratchPort = Scratch postgres "SELECT inet_server_port() || '|' || current_setting('server_version')"
"cluster jetable : $scratchPort"
if ($scratchPort -notlike '55434|18.*') { throw 'cluster jetable inattendu' }
foreach ($role in 'mmv_v2_dev', 'mmv_v2_test') {
    Scratch postgres "DO `$`$BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '$role') THEN CREATE ROLE $role NOLOGIN; END IF; END`$`$;" | Out-Null
}
$targets = @(@{ Key = 'ConnectionStrings:Mmv'; Database = 'mmv_v2_dev' }, @{ Key = 'ConnectionStrings:MmvTest'; Database = 'mmv_v2_test' })
foreach ($t in $targets) {
    $raw = $null
    foreach ($sid in 'mmv-v2-api', 'mmv-v2-postgresql-tests') {
        $f = Join-Path $env:APPDATA "Microsoft\UserSecrets\$sid\secrets.json"
        if (-not $raw -and (Test-Path -LiteralPath $f)) { $raw = (Get-Content -LiteralPath $f -Raw -Encoding UTF8 | ConvertFrom-Json).($t.Key) }
    }
    $b = [System.Data.Common.DbConnectionStringBuilder]::new(); $b.set_ConnectionString($raw)
    $get = { param($names) foreach ($n in $names) { if ($b.ContainsKey($n)) { return [string]$b[$n] } }; return $null }
    if ((& $get @('Host', 'Server')) -notin @('localhost', '127.0.0.1', '::1') -or (& $get @('Port')) -ne '5433' -or (& $get @('Database')) -ne $t.Database -or (& $get @('Username', 'User ID', 'User')) -ne $t.Database) { throw "cible refusee $($t.Database)" }
    $db = $t.Database
    $dump = Join-Path $dir "${db}_avant_0009_$stamp.dump"
    $env:PGPASSWORD = & $get @('Password')
    $env:PGOPTIONS = '-c default_transaction_read_only=on -c statement_timeout=120s -c lock_timeout=5s'
    try {
        "=== $db : sauvegarde definitive avant 0009 ($((Get-Date).ToUniversalTime().ToString('s'))Z)"
        $id = & "$bin\psql.exe" -h localhost -p 5433 -U $db -d $db -X -A -t -v ON_ERROR_STOP=1 -f $IdentitySql
        $id
        if ($id -notlike "identite=$db|$db|5433|18.6|lecture_seule=on|migrations=8|0009=false|autres_sessions=0") { throw "STOP : identite, migrations ou sessions non conformes pour $db" }
        & "$bin\pg_dump.exe" -h localhost -p 5433 -U $db -d $db -Fc -f $dump
        $rc = $LASTEXITCODE; "pg_dump exit=$rc"; if ($rc -ne 0) { throw "STOP : pg_dump $db exit $rc" }
        & "$bin\psql.exe" -h localhost -p 5433 -U $db -d $db -X -A -t -v ON_ERROR_STOP=1 -f $CheckSql | Set-Content -Encoding utf8 "$dump.check.txt"
        $rc = $LASTEXITCODE; "check source exit=$rc"; if ($rc -ne 0) { throw "STOP : check source $db" }
        $id2 = & "$bin\psql.exe" -h localhost -p 5433 -U $db -d $db -X -A -t -v ON_ERROR_STOP=1 -f $IdentitySql
        "apres sauvegarde : $id2"
        if ($id2 -ne $id) { throw "STOP : etat de $db modifie pendant la sauvegarde" }
    }
    finally { Remove-Item Env:PGPASSWORD, Env:PGOPTIONS -ErrorAction SilentlyContinue }
    "sha256=$((Get-FileHash $dump -Algorithm SHA256).Hash.ToLowerInvariant()) fichier=$(Split-Path $dump -Leaf) octets=$((Get-Item $dump).Length)"
    "empreinte de coherence (fichier .check.txt) sha256=$((Get-FileHash "$dump.check.txt" -Algorithm SHA256).Hash.ToLowerInvariant())"
    $list = & "$bin\pg_restore.exe" --list $dump; $rc = $LASTEXITCODE
    "pg_restore --list exit=$rc entrees=$(@($list | Where-Object { $_ -notmatch '^;' }).Count) donnees_de_table=$(@($list | Where-Object { $_ -match 'TABLE DATA' }).Count)"
    if ($rc -ne 0) { throw "STOP : pg_restore --list $db" }
    $list | Set-Content -Encoding utf8 "$dump.list.txt"
    $fresh = "gd_$db"
    Scratch postgres "DROP DATABASE IF EXISTS $fresh" | Out-Null
    Scratch postgres "CREATE DATABASE $fresh WITH OWNER $db TEMPLATE template0 ENCODING 'UTF8' LC_COLLATE 'C' LC_CTYPE 'C'" | Out-Null
    & "$bin\pg_restore.exe" -h 127.0.0.1 -p 55434 -U postgres -d $fresh --exit-on-error --single-transaction $dump
    $rc = $LASTEXITCODE; "restauration (cluster jetable 55434, base neuve $fresh) exit=$rc"; if ($rc -ne 0) { throw "STOP : restauration $db" }
    & "$bin\psql.exe" -h 127.0.0.1 -p 55434 -U postgres -d $fresh -X -A -t -v ON_ERROR_STOP=1 -f $CheckSql | Set-Content -Encoding utf8 (Join-Path $Work "$db.restored.check.txt")
    Copy-Item "$dump.check.txt" (Join-Path $Work "$db.source.check.txt")
    $src = Get-Content "$dump.check.txt"; $dst = Get-Content (Join-Path $Work "$db.restored.check.txt")
    $diff = Compare-Object $src $dst
    "comparaison source / restauree : $(if ($diff) { 'ECART ' + @($diff).Count } else { 'IDENTIQUE' }) (tables=$(@($src | Where-Object { $_ -like 'table|*' }).Count), declencheurs=$(@($src | Where-Object { $_ -like 'trigger|*' }).Count), lignes=$($src.Count))"
    "declencheurs inactifs apres restauration=$(@($dst | Where-Object { $_ -like 'trigger|*' -and $_ -notlike '*|O' }).Count)"
    Scratch postgres "DROP DATABASE $fresh" | Out-Null; "copie restauree $fresh supprimee du cluster jetable"
    if ($diff) { throw "STOP : restauration de $db non identique" }
}
foreach ($role in 'mmv_v2_dev', 'mmv_v2_test') { Scratch postgres "DROP ROLE IF EXISTS $role" | Out-Null }
"roles miroirs supprimes du cluster jetable ; sauvegardes definitives conservees dans $dir (horodatage $stamp)"
