# GATE D (07/11/2026, reprise de P0508_purge-test.ps1) : purge des donnees FICTIVES de banc sur mmv_v2_test UNIQUEMENT (port 5433).
# Remet la base dans l'etat de reference post-0009 (tables de 0009 du schema imports comprises) : seules catalog.families (6 lignes de la migration 0007) et public.__EFMigrationsHistory sont conservees.
# Une transaction : declencheurs utilisateur desactives (droit du proprietaire), TRUNCATE sans CASCADE (toute table
# referencante oubliee = erreur, jamais une perte silencieuse), declencheurs reactives. Mot de passe jamais affiche.
param([string] $Psql = 'C:\Program Files\PostgreSQL\18\bin\psql.exe')
$ErrorActionPreference = 'Stop'
$secrets = Get-Content -LiteralPath (Join-Path $env:APPDATA 'Microsoft\UserSecrets\mmv-v2-postgresql-tests\secrets.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$b = [System.Data.Common.DbConnectionStringBuilder]::new(); $b.set_ConnectionString($secrets.'ConnectionStrings:MmvTest')
if ([string]$b['Host'] -notin @('localhost', '127.0.0.1', '::1') -or [string]$b['Port'] -ne '5433' -or [string]$b['Database'] -ne 'mmv_v2_test' -or [string]$b['Username'] -ne 'mmv_v2_test') { throw 'target refused' }
$env:PGPASSWORD = [string]$b['Password']
$sql = @'
\set ON_ERROR_STOP 1
SET lock_timeout = '5s';
BEGIN;
SELECT current_database() = 'mmv_v2_test' AND inet_server_port() = 5433 AS cible_ok \gset
\if :cible_ok
\else
  \echo 'CIBLE REFUSEE'
  \quit 3
\endif
SELECT string_agg(format('%I.%I', schemaname, tablename), ', ' ORDER BY schemaname, tablename) AS tables
FROM pg_tables
WHERE schemaname IN ('auth','clients','appointments','audit','optical','org','catalog','pricing','stock','imports')
  AND NOT (schemaname = 'catalog' AND tablename = 'families') \gset
SELECT format('ALTER TABLE %I.%I DISABLE TRIGGER USER;', schemaname, tablename)
FROM pg_tables WHERE schemaname IN ('auth','clients','appointments','audit','optical','org','catalog','pricing','stock','imports')
  AND NOT (schemaname = 'catalog' AND tablename = 'families') \gexec
TRUNCATE :tables;
SELECT format('ALTER TABLE %I.%I ENABLE TRIGGER USER;', schemaname, tablename)
FROM pg_tables WHERE schemaname IN ('auth','clients','appointments','audit','optical','org','catalog','pricing','stock','imports')
  AND NOT (schemaname = 'catalog' AND tablename = 'families') \gexec
SELECT count(*) AS declencheurs_desactives FROM pg_trigger WHERE NOT tgisinternal AND tgenabled = 'D';
COMMIT;
'@
try {
    "PURGE START $(Get-Date -Format o)"
    $sql | & $Psql -h localhost -p 5433 -U mmv_v2_test -d mmv_v2_test -X -q -P pager=off
    "PURGE EXIT $LASTEXITCODE"
    if ($LASTEXITCODE -ne 0) { throw "purge exit $LASTEXITCODE" }
}
finally { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue; $b.Clear() }
