# Prévalidation (lecture seule sur les bases du propriétaire) : sauvegarde pg_dump de mmv_v2_dev et mmv_v2_test,
# empreinte, lisibilité, puis restauration de CHAQUE sauvegarde sur une base NEUVE du cluster JETABLE (55434) et
# comparaison de l'empreinte de cohérence avec celle de la base source. Les copies restaurées sont ensuite supprimées
# du cluster jetable. Ce N'EST PAS la sauvegarde définitive d'avant application (à reprendre au GO, postes arrêtés).
param([Parameter(Mandatory)] [string] $Work, [Parameter(Mandatory)] [string] $CheckSql, [string] $ReuseStamp)
$ErrorActionPreference = 'Stop'
$bin = 'C:\Program Files\PostgreSQL\18\bin'
$dir = Join-Path $env:USERPROFILE 'Documents\MMV-V2-sauvegardes'
New-Item -ItemType Directory -Force $Work | Out-Null
$env:PGCLIENTENCODING = 'UTF8'
$stamp = if ($ReuseStamp) { $ReuseStamp } else { (Get-Date).ToUniversalTime().ToString('yyyyMMdd_HHmmss') }
function Scratch([string] $db, [string] $sql) { & "$bin\psql.exe" -h 127.0.0.1 -p 55434 -U postgres -d $db -X -A -t -v ON_ERROR_STOP=1 -c $sql; if ($LASTEXITCODE -ne 0) { throw "psql jetable $LASTEXITCODE" } }
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
    $dump = Join-Path $dir "${db}_prevalidation_0009_$stamp.dump"
    $env:PGPASSWORD = & $get @('Password')
    $env:PGOPTIONS = '-c default_transaction_read_only=on -c statement_timeout=120s -c lock_timeout=5s'
    try {
        "=== $db : sauvegarde de prévalidation ($((Get-Date).ToUniversalTime().ToString('s'))Z)"
        & "$bin\psql.exe" -h localhost -p 5433 -U $db -d $db -X -A -t -v ON_ERROR_STOP=1 -f (Join-Path $PSScriptRoot 'identity.sql')
        if ($ReuseStamp) { "sauvegarde de prevalidation existante reutilisee (aucun nouveau pg_dump) : $(Split-Path $dump -Leaf)" } else { & "$bin\pg_dump.exe" -h localhost -p 5433 -U $db -d $db -Fc -f $dump; "pg_dump exit=$LASTEXITCODE" }
        & "$bin\psql.exe" -h localhost -p 5433 -U $db -d $db -X -A -t -v ON_ERROR_STOP=1 -f $CheckSql | Set-Content -Encoding utf8 (Join-Path $Work "$db.source.check.txt"); "check source exit=$LASTEXITCODE"
    }
    finally { Remove-Item Env:PGPASSWORD, Env:PGOPTIONS -ErrorAction SilentlyContinue }
    "sha256=$((Get-FileHash $dump -Algorithm SHA256).Hash.ToLowerInvariant()) fichier=$(Split-Path $dump -Leaf) octets=$((Get-Item $dump).Length)"
    $list = & "$bin\pg_restore.exe" --list $dump; "pg_restore --list exit=$LASTEXITCODE entrees=$(@($list | Where-Object { $_ -notmatch '^;' }).Count) donnees_de_table=$(@($list | Where-Object { $_ -match 'TABLE DATA' }).Count)"
    $list | Set-Content -Encoding utf8 "$dump.list.txt"
    $fresh = "pv_$db"
    Scratch postgres "DROP DATABASE IF EXISTS $fresh" | Out-Null
    Scratch postgres "CREATE DATABASE $fresh WITH OWNER $db TEMPLATE template0 ENCODING 'UTF8' LC_COLLATE 'C' LC_CTYPE 'C'" | Out-Null
    & "$bin\pg_restore.exe" -h 127.0.0.1 -p 55434 -U postgres -d $fresh --exit-on-error --single-transaction $dump; "restauration (cluster jetable, base neuve $fresh) exit=$LASTEXITCODE"
    & "$bin\psql.exe" -h 127.0.0.1 -p 55434 -U postgres -d $fresh -X -A -t -v ON_ERROR_STOP=1 -f $CheckSql | Set-Content -Encoding utf8 (Join-Path $Work "$db.restored.check.txt")
    $src = Get-Content (Join-Path $Work "$db.source.check.txt"); $dst = Get-Content (Join-Path $Work "$db.restored.check.txt")
    $diff = Compare-Object $src $dst
    "comparaison source / restauree : $(if ($diff) { 'ECART ' + @($diff).Count + ' ligne(s) : ' + (($diff | Select-Object -First 4 | ForEach-Object { ($_.InputObject -split '\|')[0..1] -join '|' }) -join ' ; ') } else { 'IDENTIQUE' }) (tables=$(@($src | Where-Object { $_ -like 'table|*' }).Count), declencheurs=$(@($src | Where-Object { $_ -like 'trigger|*' }).Count), lignes=$($src.Count))"
    "declencheurs inactifs apres restauration=$(@($dst | Where-Object { $_ -like 'trigger|*' -and $_ -notlike '*|O' }).Count)"
    Scratch postgres "DROP DATABASE $fresh" | Out-Null; "copie restauree $fresh supprimee du cluster jetable"
}
foreach ($role in 'mmv_v2_dev', 'mmv_v2_test') { Scratch postgres "DROP ROLE IF EXISTS $role" | Out-Null }
"roles miroirs supprimes du cluster jetable ; sauvegardes de prevalidation conservees dans $dir"
