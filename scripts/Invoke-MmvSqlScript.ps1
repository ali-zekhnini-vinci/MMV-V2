<#
.SYNOPSIS
  Applique un script SQL APPROUVÉ à une base MMV V2 locale, après contrôles (phase 02, GO O-02-01).

.DESCRIPTION
  - Chaîne de connexion lue dans les user-secrets existants (jamais affichée) ; mot de passe transmis au seul
    processus psql par la variable PGPASSWORD, effacée ensuite.
  - Refus SANS connexion si : hôte non loopback, port différent de 5433, base ou rôle inattendus,
    empreinte SHA-256 du script différente de la valeur approuvée.
  - Contrôle AVANT écriture (lecture seule, côté serveur) : current_database(), current_user, port, version >= 18.
  - Exécution : psql -v ON_ERROR_STOP=1 -f <script>. Les scripts EF Core sont transactionnels
    (START TRANSACTION … COMMIT) : en cas d'erreur, rien n'est appliqué.
  - Délai d'attente de verrou (04-B) : lock_timeout transmis par PGOPTIONS (défaut 5s, -LockTimeout) ; un verrou
    non obtenu à temps fait échouer le script, transaction annulée, au lieu de bloquer les postes.
  - Aucune migration au démarrage de l'API, aucun EnsureCreated().
  - Après COMMIT : liste des migrations relue en lecture seule (requête transmise par l'entrée standard, O-03-16).
  - Tests (psql simulé, aucune connexion) : scripts/tests/Invoke-MmvSqlScript.Tests.ps1.

.EXAMPLE
  ./scripts/Invoke-MmvSqlScript.ps1 -SecretsId mmv-v2-api -Key ConnectionStrings:Mmv -Database mmv_v2_dev `
      -Script docs/phase/02/sql/0002_AuthIdentity.idempotent.sql -Sha256 <empreinte approuvée>
#>
param(
    [Parameter(Mandatory)] [string] $SecretsId,
    [Parameter(Mandatory)] [string] $Key,
    [Parameter(Mandatory)] [ValidateSet('mmv_v2_dev', 'mmv_v2_test')] [string] $Database,
    [Parameter(Mandatory)] [string] $Script,
    [Parameter(Mandatory)] [string] $Sha256,
    [string] $Psql = 'C:\Program Files\PostgreSQL\18\bin\psql.exe',
    [ValidatePattern('^[1-9][0-9]{0,5}(ms|s)$')] [string] $LockTimeout = '5s'
)
$ErrorActionPreference = 'Stop'

$actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $Script).Hash
if ($actualHash -ne $Sha256.ToUpperInvariant()) { throw "Empreinte refusee pour $Script : $actualHash" }

$secretsFile = Join-Path $env:APPDATA "Microsoft\UserSecrets\$SecretsId\secrets.json"
$secrets = Get-Content -LiteralPath $secretsFile -Raw -Encoding UTF8 | ConvertFrom-Json
$raw = $secrets.$Key
if (-not $raw) { throw "Cle '$Key' absente de $SecretsId." }
$b = [System.Data.Common.DbConnectionStringBuilder]::new()
# Accesseur explicite : sur un IDictionary, « $b.ConnectionString = » créerait une clé au lieu d'analyser la chaîne.
$b.set_ConnectionString($raw)
$get = { param($names) foreach ($n in $names) { if ($b.ContainsKey($n)) { return [string]$b[$n] } }; return $null }
$hostName = & $get @('Host', 'Server'); $port = & $get @('Port'); $db = & $get @('Database'); $user = & $get @('Username', 'User ID', 'User')
if ($hostName -notin @('localhost', '127.0.0.1', '::1')) { throw "Hote refuse (loopback attendu)." }
if ($port -ne '5433') { throw "Port refuse (5433 attendu)." }
if ($db -ne $Database -or $user -ne $Database) { throw "Cible refusee : base et role '$Database' attendus." }

$env:PGPASSWORD = & $get @('Password')
$env:PGOPTIONS = "-c lock_timeout=$LockTimeout"
try {
    $common = @('-h', 'localhost', '-p', '5433', '-U', $Database, '-d', $Database, '-X', '-v', 'ON_ERROR_STOP=1')
    $identity = & $Psql @common -A -t -c "SELECT current_database() || '|' || current_user || '|' || inet_server_port() || '|' || current_setting('server_version_num')"
    if ($LASTEXITCODE -ne 0) { throw "Controle d'identite impossible (psql exit $LASTEXITCODE)." }
    $parts = $identity.Trim().Split('|')
    if ($parts[0] -ne $Database -or $parts[1] -ne $Database -or $parts[2] -ne '5433' -or [int]$parts[3] -lt 180000) {
        throw "Identite serveur refusee : $identity"
    }
    "PRECHECK OK : $($parts[0]) / $($parts[1]) / port $($parts[2]) / server_version_num $($parts[3])"
    "SCRIPT : $Script (SHA-256 $actualHash)"
    "LOCK_TIMEOUT : $LockTimeout"
    & $Psql @common -f $Script
    if ($LASTEXITCODE -ne 0) { throw "Echec du script (psql exit $LASTEXITCODE) : transaction annulee." }
    "APPLIQUE : exit 0"
    # O-02-15 / O-03-16 : Windows PowerShell 5.1 retire les guillemets doubles d'un argument passé à un exécutable
    # natif (psql recevait __EFMigrationsHistory sans guillemets, replié en minuscules). La requête de contrôle, en
    # lecture seule, est donc transmise par l'entrée standard, identique quelle que soit la version de PowerShell.
    # Après COMMIT, un échec de cette lecture ne remet pas l'application en cause : il est signalé, sans exception.
    $migrations = 'SELECT string_agg("MigrationId", '', '' ORDER BY "MigrationId") FROM "__EFMigrationsHistory";' | & $Psql @common -A -t
    if ($LASTEXITCODE -eq 0) {
        "MIGRATIONS : $(([string]$migrations).Trim())"
    }
    else {
        "AVERTISSEMENT : script applique (COMMIT), lecture de controle impossible (psql exit $LASTEXITCODE) : relire les migrations en lecture seule."
    }
}
finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:PGOPTIONS -ErrorAction SilentlyContinue
    $b.Clear()
}
